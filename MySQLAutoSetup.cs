using MySql.Data.MySqlClient;
using System;
using System.IO;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace WpfMySqlCrud
{
    public enum MySQLState
    {
        FullyReady,                   // Service + saved config + DB all working
        ServiceExistsNeedsConfig,     // Service running, probed a known password
        ServiceExistsUnknownPassword, // Service running but no password worked
        NotInstalled                  // No MySQL service found at all
    }

    public class MySQLAutoSetup
    {
        // ── Config ───────────────────────────────────────────────────────────
        private const string InstallPath = @"C:\WpfAppMySQL";
        private const string ServiceName = "MySQL80";
        private const string AppDatabase = "WpfCrudDB";
        private const string MySQLZipName = "mysql-8.0.45-winx64.zip";

        private static string MySQLZipPath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, MySQLZipName);

        private static readonly string LogPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mysql_setup.log");

        private static readonly string SavedConfigPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "db_config.txt");

        public Action<int, string> OnProgress { get; set; }

        // ====================================================================
        // DETECT STATE — call this first on every startup
        // ====================================================================
        public async Task<MySQLState> DetectStateAsync()
        {
            Log("=== Detecting MySQL state ===");

            bool servicePresent = IsMySQLServicePresent();
            Log($"Service present: {servicePresent}");

            // 1 — Saved config exists and works → fully ready
            if (File.Exists(SavedConfigPath))
            {
                string saved = File.ReadAllText(SavedConfigPath).Trim();
                if (!string.IsNullOrWhiteSpace(saved))
                {
                    Log("Found db_config.txt — testing...");
                    if (await TryConnectAsync(saved))
                    {
                        DatabaseHelper.ConnectionString = saved;
                        Log("Saved config OK → FullyReady");
                        return MySQLState.FullyReady;
                    }
                    Log("Saved config failed — re-probing.");
                }
            }

            // 2 — Service exists, probe common passwords
            if (servicePresent)
            {
                EnsureServiceRunning();
                Log("Service found, probing credentials...");

                foreach (string pwd in new[] { "", "root", "password", "mysql" })
                {
                    // Try app DB first
                    if (await TryConnectAsync(Build(pwd)))
                    {
                        DatabaseHelper.ConnectionString = Build(pwd);
                        File.WriteAllText(SavedConfigPath, Build(pwd));
                        Log($"Connected with pwd='{pwd}' → ServiceExistsNeedsConfig");
                        return MySQLState.ServiceExistsNeedsConfig;
                    }
                    // Try system DB (app DB may not exist yet)
                    if (await TryConnectAsync(Build(pwd, "mysql")))
                    {
                        Log($"System DB reachable pwd='{pwd}' → ServiceExistsNeedsConfig");
                        return MySQLState.ServiceExistsNeedsConfig;
                    }
                }

                Log("Service present but all probes failed → ServiceExistsUnknownPassword");
                return MySQLState.ServiceExistsUnknownPassword;
            }

            Log("No service found → NotInstalled");
            return MySQLState.NotInstalled;
        }

        // ====================================================================
        // FULL INSTALL — only called when state == NotInstalled
        // ====================================================================
        public async Task<string> EnsureMySQLAsync(string rootPassword)
        {
            Log("=== MySQL Auto-Setup Started ===");

            bool filesReady = Directory.Exists(InstallPath) &&
                              File.Exists(Path.Combine(InstallPath, "bin", "mysqld.exe"));

            // STEP 1 — Extract ZIP (skip if files already present)
            if (!filesReady)
            {
                Report(10, "Locating bundled MySQL package...");
                string zipPath = GetBundledZip();

                Report(25, "Extracting MySQL files...");
                await Task.Run(() => ExtractZip(zipPath));
            }
            else
            {
                Log("MySQL files already on disk — skipping extraction.");
                Report(25, "MySQL files already present, skipping extraction...");
            }

            // STEP 2 — Write config
            Report(42, "Writing MySQL configuration...");
            WriteMySQLConfig();

            // STEP 3 — Initialize data dir (always wipes first)
            Report(48, "Initializing MySQL data directory...");
            await Task.Run(() => InitializeDataDir());

            // STEP 4 — Register service
            Report(58, "Registering MySQL Windows service...");
            await Task.Run(() => RegisterService());
            await Task.Delay(3000);

            // STEP 5 — Verify + start
            Report(65, "Verifying service registration...");
            if (!IsMySQLServicePresent())
                throw new Exception($"Service '{ServiceName}' was not registered.\nSee: {LogPath}");

            Report(68, "Starting MySQL service...");
            await Task.Run(() => StartService());

            // STEP 6 — Wait for readiness
            Report(72, "Waiting for MySQL to be ready...");
            await WaitForReadyAsync(90);

            // STEP 7 — Set root password
            Report(80, "Setting root password...");
            await Task.Run(() => SetRootPassword(rootPassword));

            // STEP 8 — Create app database
            Report(85, "Creating application database...");
            await Task.Run(() => CreateDatabase(rootPassword));

            // STEP 9 — Grant remote access
            Report(88, "Granting remote access...");
            await Task.Run(() => GrantRemoteAccess(rootPassword));

            // STEP 10 — Firewall + network
            Report(92, "Configuring firewall and network...");
            await Task.Run(() => ConfigureNetworkAccess());

            // STEP 11 — Save config
            string connStr = Build(rootPassword);
            File.WriteAllText(SavedConfigPath, connStr);
            DatabaseHelper.ConnectionString = connStr;

            Report(100, $"Setup complete! LAN IP: {GetLocalIP()}");
            Log($"=== Finished. LAN: {GetLocalIP()}:3306 ===");

            return connStr;
        }

        // ====================================================================
        // LOCATE ZIP
        // ====================================================================
        private string GetBundledZip()
        {
            if (File.Exists(MySQLZipPath))
            {
                Log($"Found ZIP: {MySQLZipPath}");
                return MySQLZipPath;
            }
            string projectDir = Path.GetFullPath(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\.."));
            string fallback = Path.Combine(projectDir, MySQLZipName);
            if (File.Exists(fallback))
            {
                Log($"Found ZIP in project dir: {fallback}");
                return fallback;
            }
            throw new FileNotFoundException(
                $"MySQL ZIP '{MySQLZipName}' not found.\nExpected: {MySQLZipPath}", MySQLZipName);
        }

        // ====================================================================
        // EXTRACT ZIP
        // ====================================================================
        private void ExtractZip(string zipPath)
        {
            Log("Extracting ZIP...");
            if (Directory.Exists(InstallPath)) Directory.Delete(InstallPath, true);
            string tempDir = InstallPath + "_temp";
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);

            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, tempDir);
            string[] subs = Directory.GetDirectories(tempDir);
            string innerRoot = subs.Length == 1 ? subs[0] : tempDir;

            Directory.CreateDirectory(InstallPath);
            foreach (string d in Directory.GetDirectories(innerRoot, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(d.Replace(innerRoot, InstallPath));
            foreach (string f in Directory.GetFiles(innerRoot, "*", SearchOption.AllDirectories))
                File.Copy(f, f.Replace(innerRoot, InstallPath), true);
            Directory.Delete(tempDir, true);

            string mysqld = Path.Combine(InstallPath, "bin", "mysqld.exe");
            if (!File.Exists(mysqld))
                throw new Exception($"mysqld.exe missing after extraction: {mysqld}");
            Log("ZIP extracted successfully.");
        }

        // ====================================================================
        // WRITE my.ini
        // ====================================================================
        private void WriteMySQLConfig()
        {
            string baseFwd = InstallPath.Replace('\\', '/');
            string ini =
                "[mysqld]\n" +
                $"basedir={baseFwd}\n" +
                $"datadir={baseFwd}/data\n" +
                "port=3306\n" +
                "bind-address=0.0.0.0\n" +
                "max_connections=200\n" +
                "character-set-server=utf8mb4\n" +
                "collation-server=utf8mb4_unicode_ci\n" +
                "\n[mysql]\n" +
                "default-character-set=utf8mb4\n" +
                "\n[client]\n" +
                "default-character-set=utf8mb4\n";
            File.WriteAllText(Path.Combine(InstallPath, "my.ini"), ini, new UTF8Encoding(false));
            Log("my.ini written.");
        }

        // ====================================================================
        // INITIALIZE DATA DIR
        // ====================================================================
        private void InitializeDataDir()
        {
            string mysqld = Path.Combine(InstallPath, "bin", "mysqld.exe");
            string myIni = Path.Combine(InstallPath, "my.ini");
            string dataDir = Path.Combine(InstallPath, "data");

            if (Directory.Exists(dataDir))
            {
                Log("Removing stale data dir...");
                Directory.Delete(dataDir, true);
            }

            Log("Running mysqld --initialize-insecure (once)...");
            Exec(mysqld, $"--defaults-file=\"{myIni}\" --initialize-insecure --console",
                throwOnFail: true);
            Log("Data directory initialized.");
        }

        // ====================================================================
        // REGISTER SERVICE
        // ====================================================================
        private void RegisterService()
        {
            string mysqld = Path.Combine(InstallPath, "bin", "mysqld.exe");
            string myIni = Path.Combine(InstallPath, "my.ini");

            Log("Cleaning up old service entry...");
            Exec("sc", $"stop {ServiceName}", throwOnFail: false);
            System.Threading.Thread.Sleep(1000);
            Exec("sc", $"delete {ServiceName}", throwOnFail: false);
            System.Threading.Thread.Sleep(2000);

            Log("Trying mysqld --install...");
            string output = Exec(mysqld,
                $"--install {ServiceName} --defaults-file=\"{myIni}\"",
                throwOnFail: false);

            if (output.Contains("Denied", StringComparison.OrdinalIgnoreCase))
            {
                Log("Denied — trying sc create with UAC...");
                string binPath = $"\"{mysqld}\" --defaults-file=\"{myIni}\" {ServiceName}";
                bool ok = TryRunAsAdmin("sc",
                    $"create {ServiceName} binPath= \"{binPath}\" " +
                    $"DisplayName= \"MySQL 8.0\" start= auto");
                if (!ok) { ElevateAndRestart(); return; }
                Log("sc create succeeded.");
            }
            else
            {
                Log("mysqld --install succeeded.");
            }

            System.Threading.Thread.Sleep(1500);
            if (!IsMySQLServicePresent())
                throw new Exception($"Service '{ServiceName}' not visible after registration.");
        }

        // ====================================================================
        // START / ENSURE SERVICE
        // ====================================================================
        private void StartService()
        {
            Log("Starting MySQL service...");
            try
            {
                using var sc = new ServiceController(ServiceName);
                if (sc.Status == ServiceControllerStatus.Running) { Log("Already running."); return; }
                sc.Start();
                sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(60));
                Log("Service started.");
            }
            catch (Exception ex)
            {
                Log($"ServiceController failed: {ex.Message}. Trying net start...");
                Exec("net", $"start {ServiceName}", throwOnFail: true);
            }
        }

        private void EnsureServiceRunning()
        {
            try
            {
                using var sc = new ServiceController(ServiceName);
                if (sc.Status != ServiceControllerStatus.Running)
                {
                    sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                }
                Log("Service is running.");
            }
            catch { Exec("net", $"start {ServiceName}", throwOnFail: false); }
        }

        // ====================================================================
        // WAIT FOR READY
        // ====================================================================
        private async Task WaitForReadyAsync(int maxSeconds)
        {
            Log("Polling MySQL readiness...");
            // Use system 'mysql' DB — app DB doesn't exist yet at this point
            string cs = Build("", "mysql");
            for (int t = 0; t < maxSeconds; t += 2)
            {
                try
                {
                    using var c = new MySqlConnection(cs);
                    await c.OpenAsync();
                    Log($"MySQL ready after {t}s."); return;
                }
                catch (Exception ex)
                {
                    Log($"Not ready ({t}s): {ex.Message}");
                    await Task.Delay(2000);
                }
            }
            throw new Exception("MySQL did not become ready in time.");
        }

        // ====================================================================
        // SET ROOT PASSWORD
        // ====================================================================
        private void SetRootPassword(string pwd)
        {
            Log("Setting root password...");
            ExecStdin(GetMySQLClientPath(),
                "-u root --connect-expired-password --protocol=TCP --port=3306 --host=127.0.0.1",
                $"ALTER USER 'root'@'localhost' IDENTIFIED BY '{EscSQL(pwd)}';",
                "FLUSH PRIVILEGES;");
            Log("Root password set.");
        }

        // ====================================================================
        // CREATE DATABASE
        // ====================================================================
        private void CreateDatabase(string pwd)
        {
            Log($"Creating database '{AppDatabase}'...");
            ExecStdin(GetMySQLClientPath(),
                $"-u root -p\"{EscArg(pwd)}\" --protocol=TCP --port=3306 --host=127.0.0.1",
                $"CREATE DATABASE IF NOT EXISTS `{AppDatabase}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;");
            Log("Database ready.");
        }

        // ====================================================================
        // GRANT REMOTE ACCESS
        // ====================================================================
        private void GrantRemoteAccess(string pwd)
        {
            Log("Granting ALL PRIVILEGES to root@%...");
            ExecStdin(GetMySQLClientPath(),
                $"-u root -p\"{EscArg(pwd)}\" --protocol=TCP --port=3306 --host=127.0.0.1",
                $"CREATE USER IF NOT EXISTS 'root'@'%' IDENTIFIED BY '{EscSQL(pwd)}';",
                "GRANT ALL PRIVILEGES ON *.* TO 'root'@'%' WITH GRANT OPTION;",
                "FLUSH PRIVILEGES;");
            Log("Remote access granted.");
        }

        // ====================================================================
        // CONFIGURE FIREWALL + NETWORK
        // ====================================================================
        private void ConfigureNetworkAccess()
        {
            Log("Configuring network access...");
            string rule = "MySQL_LAN_Access_3306";
            Exec("netsh", $"advfirewall firewall delete rule name=\"{rule}\"", throwOnFail: false);
            Exec("netsh",
                $"advfirewall firewall add rule name=\"{rule}\" " +
                "dir=in action=allow protocol=TCP localport=3306 profile=any",
                throwOnFail: false);
            Exec("powershell",
                "-NoProfile -NonInteractive -Command \"" +
                "Get-NetConnectionProfile | ForEach-Object { " +
                "Set-NetConnectionProfile -InterfaceIndex $_.InterfaceIndex -NetworkCategory Private }\"",
                throwOnFail: false);
            Exec("netsh",
                "advfirewall firewall set rule group=\"Network Discovery\" new enable=Yes",
                throwOnFail: false);
            Log("Network configured.");
        }

        // ====================================================================
        // GET LOCAL IP
        // ====================================================================
        public string GetLocalIP()
        {
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var addr in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        string ip = addr.Address.ToString();
                        if (!ip.StartsWith("127.") && !ip.StartsWith("169.254."))
                            return ip;
                    }
                }
            }
            catch (Exception ex) { Log("GetLocalIP: " + ex.Message); }
            return "Unknown IP";
        }

        // ====================================================================
        // PROCESS HELPERS
        // ====================================================================

        /// <summary>Single executor — runs process ONCE. Returns combined output.</summary>
        private string Exec(string exe, string args, bool throwOnFail)
        {
            Log($"[EXEC] {exe} {args}");
            try
            {
                using var p = System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = exe,
                        Arguments = args,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    });
                string stdout = p.StandardOutput.ReadToEnd();
                string stderr = p.StandardError.ReadToEnd();
                p.WaitForExit();
                string combined = stdout + stderr;
                Log($"OUT: {combined.Trim()}\nExit: {p.ExitCode}");
                if (throwOnFail && p.ExitCode != 0)
                    throw new Exception(
                        $"Process failed (exit {p.ExitCode})\nCMD: {exe} {args}\nSTDERR: {stderr}");
                return combined;
            }
            catch (Exception ex) when (!throwOnFail)
            {
                Log($"[ignored] {ex.Message}");
                return ex.Message;
            }
        }

        /// <summary>Feeds SQL lines via stdin to mysql.exe.</summary>
        private void ExecStdin(string exe, string args, params string[] lines)
        {
            Log($"[STDIN] {exe} {args}");
            using var p = System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = args,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });
            foreach (var line in lines) p.StandardInput.WriteLine(line);
            p.StandardInput.WriteLine("exit");
            p.StandardInput.Close();
            string out_ = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
            p.WaitForExit(15000);
            Log($"OUT: {out_.Trim()}\nExit: {p.ExitCode}");
        }

        private bool TryRunAsAdmin(string exe, string args)
        {
            try
            {
                using var p = System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = exe,
                        Arguments = args,
                        UseShellExecute = true,
                        Verb = "runas",
                        CreateNoWindow = true
                    });
                p.WaitForExit(30000);
                return p.ExitCode == 0;
            }
            catch (Exception ex) { Log($"[ADMIN] {ex.Message}"); return false; }
        }

        private void ElevateAndRestart()
        {
            string exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
            if (exe.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                exe = Path.ChangeExtension(exe, ".exe");
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                { FileName = exe, UseShellExecute = true, Verb = "runas" });
            }
            catch (Exception ex)
            {
                throw new Exception(
                    "Administrator rights required.\n" +
                    "Right-click the app → 'Run as administrator'.\n\n" + ex.Message);
            }
            Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
        }

        // ====================================================================
        // MISC HELPERS
        // ====================================================================
        private async Task<bool> TryConnectAsync(string connStr)
        {
            try
            {
                using var c = new MySqlConnection(connStr);
                await c.OpenAsync();
                return true;
            }
            catch (Exception ex) { Log($"TryConnect failed: {ex.Message}"); return false; }
        }

        private bool IsMySQLServicePresent()
        {
            foreach (var name in new[] { "MySQL80", "MySQL", "MySQL57", "MariaDB" })
            {
                try
                {
                    using var sc = new ServiceController(name);
                    _ = sc.Status;
                    Log($"Found service: {name}");
                    return true;
                }
                catch { }
            }
            return false;
        }

        private string GetMySQLClientPath()
        {
            string local = Path.Combine(InstallPath, "bin", "mysql.exe");
            if (File.Exists(local)) return local;
            foreach (var p in new[]
            {
                @"C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe",
                @"C:\Program Files\MySQL\MySQL Server 5.7\bin\mysql.exe",
            })
                if (File.Exists(p)) return p;
            return "mysql";
        }

        /// <summary>Single connection string builder — no duplicates.</summary>
        private string Build(string pwd, string database = null) =>
            new MySqlConnectionStringBuilder
            {
                Server = "localhost",
                Port = 3306,
                UserID = "root",
                Password = pwd,
                Database = database ?? AppDatabase,
                AllowPublicKeyRetrieval = true,
                SslMode = MySqlSslMode.None,
                ConnectionTimeout = 5,
                Pooling = false
            }.ConnectionString;

        private string EscSQL(string v) => v.Replace("'", "\\'");
        private string EscArg(string v) => v.Replace("\"", "\\\"");

        private void Report(int pct, string msg)
        {
            Log($"[{pct}%] {msg}");
            OnProgress?.Invoke(pct, msg);
        }

        private void Log(string msg)
        {
            try
            {
                File.AppendAllText(LogPath,
                    $"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
                System.Diagnostics.Debug.WriteLine(msg);
            }
            catch { }
        }
    }
}