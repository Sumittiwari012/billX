using MySql.Data.MySqlClient;
using MyWPFCRUDApp.Models;
using System;
using System.Diagnostics;
using System.IO;
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

            string mysqldumpExe = string.IsNullOrWhiteSpace(settings.MySqlDumpExePath)
                ? "mysqldump" // rely on PATH
                : settings.MySqlDumpExePath;

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