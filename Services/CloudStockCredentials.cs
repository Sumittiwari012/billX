using MySql.Data.MySqlClient;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace MyWPFCRUDApp.Services
{
    /// <summary>
    /// The cloud login used ONLY by CloudStock (stock transfer writes + refresh).
    /// Asked once, tested, then stored encrypted with Windows DPAPI (current user, this PC) at:
    ///     %APPDATA%\MyWPFCRUDApp\cloudstock.dat
    /// It is separate from cloudsettings.dat, so you can give it a restricted MySQL user.
    /// The password is never shown again and never written to a log.
    /// </summary>
    public static class CloudStockCredentials
    {
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MyWPFCRUDApp", "cloudstock.dat");

        private static string? _cached;
        private static readonly object Gate = new object();

        public static bool IsSaved => Get() != null;

        /// <summary>The saved connection string, or null if none (or it cannot be decrypted on this PC/user).</summary>
        public static string? Get()
        {
            lock (Gate)
            {
                if (_cached != null) return _cached;
                try
                {
                    if (!File.Exists(FilePath)) return null;
                    var plain = ProtectedData.Unprotect(
                        File.ReadAllBytes(FilePath), null, DataProtectionScope.CurrentUser);
                    _cached = Encoding.UTF8.GetString(plain);
                    return _cached;
                }
                catch { return null; }
            }
        }

        /// <summary>Returns the saved login; if there is none, asks for it once.</summary>
        public static string GetOrPrompt()
        {
            var cs = Get();
            if (cs != null) return cs;
            if (!Prompt())
                throw new InvalidOperationException("The cloud login was not entered, so nothing was changed.");
            return Get()!;
        }

        /// <summary>Shows the login window (also use this from a "Change cloud password" button).</summary>
        public static bool Prompt()
        {
            var d = Application.Current?.Dispatcher;
            if (d == null) return false;
            return d.CheckAccess() ? ShowDialog() : d.Invoke(ShowDialog);
        }

        public static void Clear()
        {
            lock (Gate)
            {
                _cached = null;
                try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch { }
            }
        }

        private static void Save(string connectionString)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var protectedBytes = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(connectionString), null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(FilePath, protectedBytes);
            lock (Gate) _cached = connectionString;
        }

        private static bool ShowDialog()
        {
            // Pre-fill server / port / database from the normal cloud settings, never the user or password.
            string server = "", database = "";
            uint port = 3306;
            try
            {
                var b = new MySqlConnectionStringBuilder(CloudSyncService.CloudConnectionString ?? "");
                server = b.Server; database = b.Database; port = b.Port;
            }
            catch { }

            var tbServer = new TextBox { Text = server };
            var tbPort = new TextBox { Text = port.ToString() };
            var tbDb = new TextBox { Text = database };
            var tbUser = new TextBox();
            var pb = new PasswordBox();
            var status = new TextBlock
            {
                Foreground = System.Windows.Media.Brushes.Firebrick,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0)
            };
            var ok = new Button { Content = "Test and save", IsDefault = true, Padding = new Thickness(14, 4, 14, 4), Margin = new Thickness(0, 0, 8, 0) };
            var cancel = new Button { Content = "Cancel", IsCancel = true, Padding = new Thickness(14, 4, 14, 4) };

            var panel = new StackPanel { Margin = new Thickness(16), MinWidth = 340 };
            void Add(string label, Control c)
            {
                panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 8, 0, 2) });
                panel.Children.Add(c);
            }
            panel.Children.Add(new TextBlock
            {
                Text = "Login used to save stock transfers to the cloud. It is asked once and stored encrypted on this PC.",
                TextWrapping = TextWrapping.Wrap
            });
            Add("Server", tbServer);
            Add("Port", tbPort);
            Add("Database", tbDb);
            Add("User", tbUser);
            Add("Password", pb);
            panel.Children.Add(status);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            panel.Children.Add(buttons);

            var win = new Window
            {
                Title = "Cloud stock login",
                Content = panel,
                SizeToContent = SizeToContent.WidthAndHeight,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };
            var main = Application.Current?.MainWindow;
            if (main != null && main.IsVisible && !ReferenceEquals(main, win))
            {
                win.Owner = main;
                win.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }

            var saved = false;
            ok.Click += (s, e) =>
            {
                if (!uint.TryParse(tbPort.Text.Trim(), out var p)) { status.Text = "Port must be a number."; return; }
                if (tbServer.Text.Trim() == "" || tbDb.Text.Trim() == "" || tbUser.Text.Trim() == "" || pb.Password.Length == 0)
                { status.Text = "Fill in every field."; return; }

                var csb = new MySqlConnectionStringBuilder
                {
                    Server = tbServer.Text.Trim(),
                    Port = p,
                    Database = tbDb.Text.Trim(),
                    UserID = tbUser.Text.Trim(),
                    Password = pb.Password
                };

                try
                {
                    using var c = new MySqlConnection(csb.ConnectionString);
                    c.Open();
                }
                catch (Exception ex) { status.Text = "Could not connect: " + ex.Message; return; }

                Save(csb.ConnectionString);
                saved = true;
                win.DialogResult = true;
            };

            win.ShowDialog();
            return saved;
        }
    }
}

// =====================================================================================================
// OPTIONAL: restricted MySQL user for this login (run on the cloud database as an admin)
//
//   CREATE USER 'stockapp'@'%' IDENTIFIED BY '<strong password>';
//   GRANT SELECT, INSERT, UPDATE, DELETE ON yourdb.StockTransfer     TO 'stockapp'@'%';
//   GRANT SELECT, INSERT, UPDATE, DELETE ON yourdb.StockTransferItem TO 'stockapp'@'%';
//   GRANT SELECT, INSERT, UPDATE         ON yourdb.GodownStock       TO 'stockapp'@'%';
//   GRANT SELECT, INSERT, UPDATE         ON yourdb.StoreStock        TO 'stockapp'@'%';
//   GRANT SELECT, INSERT                 ON yourdb.StockLedger       TO 'stockapp'@'%';
//   GRANT SELECT                         ON yourdb.MProducts         TO 'stockapp'@'%';
//   GRANT SELECT                         ON yourdb.MGodown           TO 'stockapp'@'%';
//   GRANT SELECT                         ON yourdb.MStore            TO 'stockapp'@'%';
//
// Many hosting panels only grant per database, not per table. Then create a separate database user for this
// app anyway, so you can change or revoke its password without touching the normal one.
// The cloud server must also allow remote connections from your PC's IP address.
// =====================================================================================================
