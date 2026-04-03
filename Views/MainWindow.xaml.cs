using MyWPFCRUDApp.Services;
using MyWPFCRUDApp.ViewModels;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using WpfMySqlCrud;

namespace MyWPFCRUDApp.Views
{
    public partial class MainWindow : Window
    {
        private MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;

            MaxHeight = SystemParameters.WorkArea.Height;
            MaxWidth = SystemParameters.WorkArea.Width;

            // Don't manually set Height/Width — let Maximized state handle it correctly
            this.WindowState = WindowState.Maximized;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            bool restarting = await AutoUpdater.CheckAndUpdateAsync(this);
            if (restarting) return;

            var setup = new MySQLAutoSetup();
            var state = await setup.DetectStateAsync();

            switch (state)
            {
                case MySQLState.FullyReady:
                    DatabaseInitializer.InitializeAllTables();
                    InitViewModel();
                    return;

                case MySQLState.ServiceExistsNeedsConfig:
                case MySQLState.ServiceExistsUnknownPassword:
                    this.Hide();
                    await HandleExistingMySQLAsync(setup, state);
                    return;

                case MySQLState.NotInstalled:
                    this.Hide();
                    await HandleFreshInstallAsync(setup);
                    return;
            }
        }

        private void InitViewModel()
        {
            DataContext = new MainViewModel();
            this.Show();
        }

        private async Task HandleExistingMySQLAsync(MySQLAutoSetup setup, MySQLState state)
        {
            string hint = state == MySQLState.ServiceExistsNeedsConfig
                ? "MySQL is installed. Enter your root password to connect:"
                : "MySQL is installed but the password couldn't be detected.\nEnter your root password:";

            MessageBox.Show(hint, "MySQL Found", MessageBoxButton.OK, MessageBoxImage.Information);

            string password = PromptForPassword();
            if (string.IsNullOrWhiteSpace(password))
            { Application.Current.Shutdown(); return; }

            string connStr = new MySql.Data.MySqlClient.MySqlConnectionStringBuilder
            {
                Server = "localhost",
                Port = 3306,
                UserID = "root",
                Password = password,
                Database = "WpfCrudDB",
                AllowPublicKeyRetrieval = true,
                SslMode = MySql.Data.MySqlClient.MySqlSslMode.None,
                ConnectionTimeout = 10
            }.ConnectionString;

            try
            {
                string sysConn = new MySql.Data.MySqlClient.MySqlConnectionStringBuilder
                {
                    Server = "localhost",
                    Port = 3306,
                    UserID = "root",
                    Password = password,
                    Database = "mysql",
                    AllowPublicKeyRetrieval = true,
                    SslMode = MySql.Data.MySqlClient.MySqlSslMode.None
                }.ConnectionString;

                using (var conn = new MySql.Data.MySqlClient.MySqlConnection(sysConn))
                {
                    await conn.OpenAsync();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText =
                        "CREATE DATABASE IF NOT EXISTS `WpfCrudDB` " +
                        "CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";
                    await cmd.ExecuteNonQueryAsync();
                }

                File.WriteAllText(
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "db_config.txt"),
                    connStr);
                DatabaseHelper.ConnectionString = connStr;

                DatabaseInitializer.InitializeAllTables();
                InitViewModel();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not connect:\n{ex.Message}",
                    "Connection Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }

        private async Task HandleFreshInstallAsync(MySQLAutoSetup setup)
        {
            string password = PromptForPassword();
            if (string.IsNullOrWhiteSpace(password))
            { Application.Current.Shutdown(); return; }

            var progressWindow = new SetupProgressWindow();
            progressWindow.Show();

            try
            {
                setup.OnProgress = (pct, msg) =>
                    Dispatcher.Invoke(() => progressWindow.Update(pct, msg));

                await setup.EnsureMySQLAsync(password);
                DatabaseInitializer.InitializeAllTables();
                progressWindow.Close();

                string lanIp = setup.GetLocalIP();
                MessageBox.Show(
                    $"MySQL installed and ready!\n\n" +
                    $"Other devices can connect using:\n" +
                    $"  Host : {lanIp}\n  Port : 3306\n" +
                    $"  User : root\n  DB   : WpfCrudDB",
                    "Setup Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                InitViewModel();
            }
            catch (Exception ex)
            {
                progressWindow.Close();
                MessageBox.Show(
                    $"MySQL setup failed:\n\n{ex.Message}\n\nCheck mysql_setup.log for details.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }

        private string PromptForPassword()
        {
            var dlg = new PasswordInputDialog();
            dlg.ShowDialog();
            return dlg.Confirmed ? dlg.EnteredPassword : string.Empty;
        }
        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private double _normalLeft, _normalTop, _normalWidth, _normalHeight;

        private void MaximizeBtn_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
                // Restore to a reasonable centered window
                this.Width = 1100;
                this.Height = 680;
                this.Left = (SystemParameters.WorkArea.Width - 1100) / 2;
                this.Top = (SystemParameters.WorkArea.Height - 680) / 2;
            }
            else
            {
                this.Left = SystemParameters.WorkArea.Left;
                this.Top = SystemParameters.WorkArea.Top;
                this.WindowState = WindowState.Maximized;
            }
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}