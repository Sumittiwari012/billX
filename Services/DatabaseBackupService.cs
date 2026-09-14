using MySql.Data.MySqlClient;
using Microsoft.Win32;
using MyWPFCRUDApp.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;

namespace MyWPFCRUDApp.Services
{
    // ════════════════════════════════════════════════════════════════════════
    // DatabaseBackupService — creates a self-contained MySQL dump (.sql file:
    // schema + data + routines + triggers, one file, fully restorable) and
    // optionally emails it. Called both from the "Backup Now" button on the
    // Backup Settings screen and automatically when the app closes.
    //
    // Every public method returns a BackupResult instead of throwing, on
    // purpose: a backup failure should be visible to the user, but it must
    // never be the reason the application fails to close.
    // ════════════════════════════════════════════════════════════════════════
    public static class DatabaseBackupService
    {
        public class BackupResult
        {
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
            public string? FilePath { get; set; }
        }

        // ── Step 1: dump the database to a single .sql file ────────────────
        public static BackupResult CreateBackup(BackupSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.BackupFolderPath))
                return Fail("No backup folder configured. Set one in Settings → Backup.");

            try
            {
                Directory.CreateDirectory(settings.BackupFolderPath);
            }
            catch (Exception ex)
            {
                return Fail($"Could not create/access backup folder: {ex.Message}");
            }

            var (host, port, database, user, password) = GetConnectionDetails();
            if (string.IsNullOrWhiteSpace(database))
                return Fail("Could not read the MySQL connection details from DatabaseHelper.ConnectionString.");

            string outputPath = Path.Combine(settings.BackupFolderPath,
                string.IsNullOrWhiteSpace(settings.BackupFileName) ? "LatestBackup.sql" : settings.BackupFileName);

            // Auto-locate mysqldump.exe instead of assuming it's on PATH or
            // relying on a fixed configured path — MySQL's install location
            // (and even whether it's on PATH at all) varies machine to
            // machine, which is exactly what caused "The system cannot find
            // the file specified" here: the app's own working directory was
            // being searched instead of anywhere MySQL is actually installed.
            string mysqldumpExe = string.IsNullOrWhiteSpace(settings.MySqlDumpExePath)
                ? FindMySqlDumpExe()
                : settings.MySqlDumpExePath;

            if (string.IsNullOrWhiteSpace(mysqldumpExe))
                return Fail(
                    "Could not find mysqldump.exe anywhere on this computer (checked PATH, the " +
                    "Windows registry, and common MySQL/XAMPP/WAMP install folders). " +
                    "Install MySQL Server (or make sure mysqldump.exe is reachable), " +
                    "or set its exact path manually in Settings → Backup.");

            // Credentials go into a short-lived "defaults extra file" rather
            // than straight onto the command line, so the MySQL password
            // doesn't show up in Task Manager / process list while the dump
            // runs. Deleted in the finally block below either way.
            string tempCnfPath = Path.Combine(Path.GetTempPath(), $"backupcreds_{Guid.NewGuid():N}.cnf");
            try
            {
                File.WriteAllText(tempCnfPath,
                    "[client]\r\n" +
                    $"user={user}\r\n" +
                    $"password={password}\r\n" +
                    $"host={host}\r\n" +
                    $"port={port}\r\n");

                var psi = new ProcessStartInfo
                {
                    FileName = mysqldumpExe,
                    Arguments =
                        $"--defaults-extra-file=\"{tempCnfPath}\" " +
                        "--single-transaction --routines --triggers --events " +
                        $"--result-file=\"{outputPath}\" " +
                        $"\"{database}\"",
                    // Explicit working directory so mysqldump.exe never
                    // inherits the app's own folder (e.g. "C:\Program Files
                    // (x86)\HP\BillIX") as its working directory. Use the
                    // exe's own folder when we resolved a full path to it;
                    // otherwise fall back to the Windows system directory,
                    // which always exists.
                    WorkingDirectory = Path.IsPathRooted(mysqldumpExe)
                        ? (Path.GetDirectoryName(mysqldumpExe) ?? Environment.SystemDirectory)
                        : Environment.SystemDirectory,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null)
                    return Fail($"Could not start mysqldump at \"{mysqldumpExe}\". Set the correct path in Settings → Backup.");

                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0 || !File.Exists(outputPath))
                    return Fail($"mysqldump failed (exit code {process.ExitCode}): {stderr}");

                return new BackupResult { Success = true, Message = "Backup file created.", FilePath = outputPath };
            }
            catch (Exception ex)
            {
                return Fail($"Backup failed: {ex.Message}");
            }
            finally
            {
                try { if (File.Exists(tempCnfPath)) File.Delete(tempCnfPath); } catch { /* best effort cleanup */ }
            }
        }

        // ── Step 2: email the backup file as an attachment ─────────────────
        public static BackupResult SendBackupEmail(BackupSettings settings, string filePath)
        {
            if (string.IsNullOrWhiteSpace(settings.BackupEmail))
                return Fail("No backup email address configured.");
            if (string.IsNullOrWhiteSpace(settings.SmtpHost))
                return Fail("No SMTP server configured.");
            if (!File.Exists(filePath))
                return Fail($"Backup file not found at \"{filePath}\".");

            try
            {
                using var message = new MailMessage
                {
                    From = new MailAddress(settings.SmtpUsername, settings.SenderDisplayName),
                    Subject = $"Database backup — {DateTime.Now:dd-MMM-yyyy HH:mm}",
                    Body = "Automated database backup attached, taken when the application was closed."
                };
                message.To.Add(settings.BackupEmail);
                message.Attachments.Add(new Attachment(filePath));

                using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort)
                {
                    EnableSsl = settings.SmtpUseSsl,
                    Credentials = new NetworkCredential(settings.SmtpUsername, settings.SmtpPassword)
                };
                client.Send(message);

                return new BackupResult { Success = true, Message = "Backup emailed.", FilePath = filePath };
            }
            catch (Exception ex)
            {
                return Fail($"Sending backup email failed: {ex.Message}");
            }
        }

        // ── Orchestrator: what actually runs when the app closes ───────────
        public static BackupResult RunCloseTimeBackup(BackupSettings settings)
        {
            if (!settings.EnableBackupOnClose)
                return new BackupResult { Success = true, Message = "Backup on close is disabled." };

            var backupResult = CreateBackup(settings);
            if (!backupResult.Success) return backupResult;

            var emailResult = SendBackupEmail(settings, backupResult.FilePath!);
            if (!emailResult.Success)
            {
                // The local file did get written and replaced even though the
                // email failed — say so, rather than reporting a flat failure
                // that implies nothing happened.
                return new BackupResult
                {
                    Success = false,
                    Message = $"Local backup saved, but emailing it failed: {emailResult.Message}",
                    FilePath = backupResult.FilePath
                };
            }

            return new BackupResult { Success = true, Message = "Backup saved and emailed.", FilePath = backupResult.FilePath };
        }

        private static BackupResult Fail(string message) => new() { Success = false, Message = message };

        // ════════════════════════════════════════════════════════════════
        // FindMySqlDumpExe — locates mysqldump.exe without assuming any
        // fixed install path, since that varies by machine (different MySQL
        // versions, XAMPP/WAMP instead of a standalone MySQL install,
        // 32-bit vs 64-bit Program Files, etc). Tried in order, cheapest
        // and most-likely-correct first:
        //
        //   1. Already on PATH — the simplest case, if it works just use it.
        //   2. The registry, where the official MySQL installer records its
        //      own install location — the most reliable source when present.
        //   3. A search across common install root folders for any
        //      "mysqldump.exe" under them (handles MySQL Server installed
        //      under Program Files, XAMPP, WAMP, or bundled with tools like
        //      HeidiSQL/Workbench that ship their own copy).
        //
        // Returns empty string if nothing is found anywhere, so the caller
        // can show a clear "couldn't find it, here's what I checked" error
        // instead of a cryptic Win32 "file not found" from Process.Start.
        // ════════════════════════════════════════════════════════════════
        private static string FindMySqlDumpExe()
        {
            // 1) Already on PATH?
            string? onPath = FindOnPath("mysqldump.exe");
            if (!string.IsNullOrEmpty(onPath)) return onPath;

            // 2) Ask the registry where MySQL Server installed itself.
            string? fromRegistry = FindViaRegistry();
            if (!string.IsNullOrEmpty(fromRegistry)) return fromRegistry;

            // 3) Fall back to scanning common install roots.
            string[] rootsToScan =
            {
                @"C:\Program Files\MySQL",
                @"C:\Program Files (x86)\MySQL",
                @"C:\xampp\mysql\bin",
                @"C:\wamp64\bin\mysql",
                @"C:\wamp\bin\mysql",
                @"C:\MySQL",
            };

            foreach (var root in rootsToScan)
            {
                var found = SearchForFile(root, "mysqldump.exe", maxDepth: 3);
                if (found != null) return found;
            }

            return string.Empty;
        }

        // Checks every folder on the PATH environment variable for the file,
        // same resolution order Windows itself would use to run a bare
        // command name.
        private static string? FindOnPath(string fileName)
        {
            string? pathVar = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathVar)) return null;

            foreach (var dir in pathVar.Split(Path.PathSeparator))
            {
                try
                {
                    string candidate = Path.Combine(dir.Trim(), fileName);
                    if (File.Exists(candidate)) return candidate;
                }
                catch
                {
                    // Malformed PATH entry — skip it and keep checking the rest.
                }
            }
            return null;
        }

        // The official MySQL Installer writes its install location under
        // this registry key. Not every install goes through the official
        // installer (XAMPP/WAMP/portable installs won't have this), which is
        // why this is tried before, not instead of, the folder scan below.
        private static string? FindViaRegistry()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\MySQL AB", writable: false)
                    ?? Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\MySQL AB", writable: false);

                if (key == null) return null;

                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    using var subKey = key.OpenSubKey(subKeyName);
                    string? location = subKey?.GetValue("Location") as string;
                    if (string.IsNullOrWhiteSpace(location)) continue;

                    string candidate = Path.Combine(location, "bin", "mysqldump.exe");
                    if (File.Exists(candidate)) return candidate;
                }
            }
            catch
            {
                // Registry access can fail for all sorts of environment
                // reasons — just fall through to the folder scan.
            }
            return null;
        }

        // Depth-limited recursive search so this can't accidentally walk an
        // entire huge drive tree — MySQL installs are never more than a
        // couple of folders deep from the roots we scan (e.g.
        // "MySQL\MySQL Server 8.0\bin\mysqldump.exe" is 2 levels deep).
        private static string? SearchForFile(string rootDir, string fileName, int maxDepth)
        {
            if (!Directory.Exists(rootDir)) return null;

            try
            {
                var direct = Directory.GetFiles(rootDir, fileName, SearchOption.TopDirectoryOnly);
                if (direct.Length > 0) return direct[0];

                if (maxDepth <= 0) return null;

                foreach (var subDir in Directory.GetDirectories(rootDir))
                {
                    var found = SearchForFile(subDir, fileName, maxDepth - 1);
                    if (found != null) return found;
                }
            }
            catch
            {
                // Permission-denied or similar on some subfolder — skip it,
                // don't let one bad folder abort the whole search.
            }
            return null;
        }

        // Reads host/port/database/user/password straight out of
        // DatabaseHelper.ConnectionString, the same connection string every
        // other Service in the app already connects with.
        private static (string Host, string Port, string Database, string User, string Password) GetConnectionDetails()
        {
            string connStr = DatabaseHelper.ConnectionString;
            if (string.IsNullOrWhiteSpace(connStr))
                return (string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);

            var builder = new MySqlConnectionStringBuilder(connStr);
            return (builder.Server, builder.Port.ToString(), builder.Database, builder.UserID, builder.Password);
        }
    }
}