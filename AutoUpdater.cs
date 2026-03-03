// ============================================================================
// AutoUpdater.cs
// Checks GitHub Releases for a newer version on every app startup.
// If found:
//   - Shows a dialog with changelog
//   - Downloads the new .zip from GitHub Release assets
//   - Extracts it next to the current .exe
//   - Relaunches the updated app and exits the old one
//
// HOW TO USE:
//   1. Set GITHUB_OWNER and GITHUB_REPO below to your GitHub username + repo
//   2. Set your app version in AssemblyInfo.cs or .csproj (e.g. <Version>1.0.0</Version>)
//   3. Call AutoUpdater.CheckAndUpdateAsync(this) from MainWindow_Loaded
//   4. When you want to release an update:
//        a. Bump <Version> in .csproj
//        b. Build → Publish as a ZIP
//        c. Go to GitHub → Releases → New Release
//        d. Tag: v1.1.0  (must start with 'v')
//        e. Attach the ZIP file as a release asset
//        f. Publish — users get it on next launch automatically
// ============================================================================

using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace WpfMySqlCrud
{
    public static class AutoUpdater
    {
        // ── CONFIGURE THESE ──────────────────────────────────────────────────
        private const string GITHUB_OWNER = "Sumittiwari012";   // ← change this
        private const string GITHUB_REPO = "billX";         // ← change this
        // ─────────────────────────────────────────────────────────────────────

        private static readonly string ApiUrl =
            $"https://api.github.com/repos/{GITHUB_OWNER}/{GITHUB_REPO}/releases/latest";

        private static readonly string LogPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update.log");

        // ====================================================================
        // MAIN ENTRY — call this from MainWindow_Loaded BEFORE setup runs
        // ====================================================================

        /// <summary>
        /// Checks GitHub for a newer release. If found, prompts the user,
        /// downloads, extracts, and relaunches. Returns true if app is
        /// restarting (caller should return immediately and not continue).
        /// </summary>
        public static async Task<bool> CheckAndUpdateAsync(Window owner = null)
        {
            try
            {
                Log("Checking for updates...");

                Version current = GetCurrentVersion();
                Log($"Current version: {current}");

                ReleaseInfo latest = await FetchLatestReleaseAsync();
                if (latest == null)
                {
                    Log("Could not fetch release info (no internet or no releases yet).");
                    return false;
                }

                Log($"Latest version on GitHub: {latest.Version}");

                if (latest.Version <= current)
                {
                    Log("Already up to date.");
                    return false;
                }

                // ── New version available ────────────────────────────────────
                Log($"Update available: {current} → {latest.Version}");

                bool accepted = ShowUpdateDialog(owner, current, latest);
                if (!accepted)
                {
                    Log("User skipped update.");
                    return false;
                }

                // ── Download + apply ─────────────────────────────────────────
                await DownloadAndApplyUpdateAsync(owner, latest);
                return true;   // app is restarting
            }
            catch (Exception ex)
            {
                Log($"Update check failed (non-fatal): {ex.Message}");
                return false;  // never block startup due to update errors
            }
        }

        // ====================================================================
        // FETCH LATEST RELEASE FROM GITHUB API
        // ====================================================================
        private static async Task<ReleaseInfo> FetchLatestReleaseAsync()
        {
            using var client = new HttpClient();
            // GitHub API requires a User-Agent header
            client.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue(GITHUB_REPO, "1.0"));
            client.Timeout = TimeSpan.FromSeconds(10);

            HttpResponseMessage response;
            try { response = await client.GetAsync(ApiUrl); }
            catch { return null; }  // no internet

            if (!response.IsSuccessStatusCode)
            {
                Log($"GitHub API returned {response.StatusCode}");
                return null;
            }

            string json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Parse tag_name like "v1.2.3" → Version 1.2.3
            string tag = root.GetProperty("tag_name").GetString() ?? "";
            string versionStr = tag.TrimStart('v');
            if (!Version.TryParse(versionStr, out Version ver)) return null;

            // Changelog from release body
            string changelog = "";
            if (root.TryGetProperty("body", out var bodyEl))
                changelog = bodyEl.GetString() ?? "";

            // Find the ZIP asset URL
            string assetUrl = null;
            string assetName = null;
            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    string name = asset.GetProperty("name").GetString() ?? "";
                    if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        assetUrl = asset.GetProperty("browser_download_url").GetString();
                        assetName = name;
                        break;
                    }
                }
            }

            if (assetUrl == null)
            {
                Log("No ZIP asset found in latest release.");
                return null;
            }

            return new ReleaseInfo
            {
                Version = ver,
                Tag = tag,
                AssetUrl = assetUrl,
                AssetName = assetName,
                Changelog = changelog
            };
        }

        // ====================================================================
        // SHOW UPDATE DIALOG
        // ====================================================================
        private static bool ShowUpdateDialog(Window owner, Version current, ReleaseInfo latest)
        {
            string changelog = string.IsNullOrWhiteSpace(latest.Changelog)
                ? "(No changelog provided)"
                : latest.Changelog.Length > 800
                    ? latest.Changelog[..800] + "\n..."
                    : latest.Changelog;

            string message =
                $"A new version is available!\n\n" +
                $"  Current : v{current}\n" +
                $"  New     : v{latest.Version}\n\n" +
                $"What's new:\n{changelog}\n\n" +
                $"Download and install now?";

            var result = MessageBox.Show(
                owner, message,
                "Update Available",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            return result == MessageBoxResult.Yes;
        }

        // ====================================================================
        // DOWNLOAD + APPLY UPDATE
        // ====================================================================
        private static async Task DownloadAndApplyUpdateAsync(Window owner, ReleaseInfo release)
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string zipPath = Path.Combine(Path.GetTempPath(), release.AssetName);

            // ── Show a simple download progress window ───────────────────────
            var progress = new SetupProgressWindow();
            progress.Show();

            try
            {
                // 1 — Download ZIP
                progress.Update(5, $"Downloading {release.AssetName}...");
                Log($"Downloading from: {release.AssetUrl}");

                await DownloadFileAsync(release.AssetUrl, zipPath,
                    (pct, msg) => owner?.Dispatcher.Invoke(() => progress.Update(pct, msg)));

                Log($"Download complete: {zipPath}");

                // 2 — Extract to a temp staging folder
                progress.Update(80, "Extracting update...");
                string stagingDir = Path.Combine(Path.GetTempPath(), $"update_{release.Tag}");
                if (Directory.Exists(stagingDir)) Directory.Delete(stagingDir, true);
                ZipFile.ExtractToDirectory(zipPath, stagingDir);

                // 3 — Write an updater batch script that:
                //     a) waits for this process to exit
                //     b) copies new files over the old ones
                //     c) relaunches the app
                //     d) deletes itself
                progress.Update(90, "Preparing update installer...");
                string exePath = GetCurrentExePath();
                string updaterBat = Path.Combine(Path.GetTempPath(), "apply_update.bat");

                // Flatten nested folder in ZIP if present (same as MySQL ZIP logic)
                string[] subs = Directory.GetDirectories(stagingDir);
                string sourceDir = (subs.Length == 1) ? subs[0] : stagingDir;

                File.WriteAllText(updaterBat,
                    $"@echo off\r\n" +
                    $"echo Waiting for app to close...\r\n" +
                    $":waitloop\r\n" +
                    $"tasklist /FI \"PID eq {Environment.ProcessId}\" 2>NUL | find /I \"{Environment.ProcessId}\" >NUL\r\n" +
                    $"if not errorlevel 1 (\r\n" +
                    $"    timeout /t 1 /nobreak >NUL\r\n" +
                    $"    goto waitloop\r\n" +
                    $")\r\n" +
                    $"echo Copying new files...\r\n" +
                    $"xcopy /E /Y /I \"{sourceDir}\\*\" \"{appDir}\"\r\n" +
                    $"echo Launching updated app...\r\n" +
                    $"start \"\" \"{exePath}\"\r\n" +
                    $"echo Cleaning up...\r\n" +
                    $"rd /S /Q \"{stagingDir}\"\r\n" +
                    $"del \"{zipPath}\"\r\n" +
                    $"del \"%~f0\"\r\n");

                // 4 — Launch the batch script hidden, then exit this process
                progress.Update(100, "Launching update...");
                Log("Launching updater batch and exiting...");

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{updaterBat}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });

                progress.Close();
                Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
            }
            catch (Exception ex)
            {
                progress.Close();
                Log($"Update apply failed: {ex.Message}");
                MessageBox.Show(
                    $"Update download failed:\n{ex.Message}\n\n" +
                    $"You can manually download from:\n" +
                    $"https://github.com/{GITHUB_OWNER}/{GITHUB_REPO}/releases",
                    "Update Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ====================================================================
        // DOWNLOAD WITH PROGRESS
        // ====================================================================
        private static async Task DownloadFileAsync(
            string url, string destPath, Action<int, string> onProgress)
        {
            using var handler = new HttpClientHandler { AllowAutoRedirect = true };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(30) };
            client.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue(GITHUB_REPO, "1.0"));

            using var response = await client.GetAsync(
                url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            long? total = response.Content.Headers.ContentLength;
            using var src = await response.Content.ReadAsStreamAsync();
            using var dest = new FileStream(destPath, FileMode.Create,
                                            FileAccess.Write, FileShare.None,
                                            81920, useAsync: true);
            byte[] buf = new byte[81920];
            long recv = 0;
            int chunk;

            while ((chunk = await src.ReadAsync(buf, 0, buf.Length)) > 0)
            {
                await dest.WriteAsync(buf, 0, chunk);
                recv += chunk;
                if (total > 0)
                {
                    int pct = (int)(5 + (recv / (double)total) * 74);
                    onProgress?.Invoke(pct,
                        $"Downloading... {recv / 1024 / 1024}MB / {total / 1024 / 1024}MB");
                }
            }
        }

        // ====================================================================
        // HELPERS
        // ====================================================================
        private static Version GetCurrentVersion()
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version
                 ?? new Version(0, 0, 0, 0);
            // Return only Major.Minor.Build to match GitHub tag format (1.2.3)
            return new Version(v.Major, v.Minor, v.Build);
        }

        private static string GetCurrentExePath()
        {
            string loc = Assembly.GetExecutingAssembly().Location;
            // .NET 6+ single-file: .Location returns .dll, swap to .exe
            if (loc.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                loc = Path.ChangeExtension(loc, ".exe");
            return loc;
        }

        private static void Log(string msg)
        {
            try
            {
                File.AppendAllText(LogPath,
                    $"[{DateTime.Now:HH:mm:ss}] [Updater] {msg}{Environment.NewLine}");
            }
            catch { }
        }

        // ====================================================================
        // DATA
        // ====================================================================
        private class ReleaseInfo
        {
            public Version Version { get; set; }
            public string Tag { get; set; }
            public string AssetUrl { get; set; }
            public string AssetName { get; set; }
            public string Changelog { get; set; }
        }
    }
}