// ============================================================================
// MainWindow.xaml.cs — Wire up MySQLAutoSetup on startup
// ============================================================================

using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace WpfMySqlCrud
{
    public partial class MainWindow : Window
    {
        private readonly DatabaseHelper _db = new();
        private int _selectedId = -1;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        // ── App Startup ──────────────────────────────────────────────────────

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // ── STEP 0: Check for updates FIRST ─────────────────────────────
            // If an update is found and user accepts, app will restart.
            // Return immediately so MySQL setup doesn't run on the old version.
            bool restarting = await AutoUpdater.CheckAndUpdateAsync(this);
            if (restarting) return;

            // ── STEP 1: MySQL setup (existing logic) ─────────────────────────
            var setup = new MySQLAutoSetup();
            var state = await setup.DetectStateAsync();

            switch (state)
            {
                // ── Already fully configured — straight to app ───────────────
                case MySQLState.FullyReady:
                    _db.EnsureTableExists();
                    LoadData();
                    return;

                // ── Service exists but config was lost — re-ask password ──────
                case MySQLState.ServiceExistsNeedsConfig:
                case MySQLState.ServiceExistsUnknownPassword:
                    this.Hide();
                    await HandleExistingMySQLAsync(setup, state);
                    return;

                // ── No MySQL at all — full install ────────────────────────────
                case MySQLState.NotInstalled:
                    this.Hide();
                    await HandleFreshInstallAsync(setup);
                    return;
            }
        }

        // ── Existing MySQL — just ask for password and save config ───────────
        private async Task HandleExistingMySQLAsync(MySQLAutoSetup setup, MySQLState state)
        {
            string hint = state == MySQLState.ServiceExistsNeedsConfig
                ? "MySQL is installed. Enter your root password to connect:"
                : "MySQL is installed but the password couldn't be detected.\nEnter your root password:";

            MessageBox.Show(hint, "MySQL Found", MessageBoxButton.OK, MessageBoxImage.Information);

            string password = PromptForPassword();
            if (string.IsNullOrWhiteSpace(password))
            {
                Application.Current.Shutdown(); return;
            }

            // Try connecting with the entered password
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
                // Create DB if it doesn't exist yet
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

                // Save and apply
                File.WriteAllText(
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "db_config.txt"),
                    connStr);
                DatabaseHelper.ConnectionString = connStr;

                _db.EnsureTableExists();
                this.Show();
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not connect with that password:\n{ex.Message}",
                    "Connection Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }

        // ── Fresh install ────────────────────────────────────────────────────
        private async Task HandleFreshInstallAsync(MySQLAutoSetup setup)
        {
            string password = PromptForPassword();
            if (string.IsNullOrWhiteSpace(password))
            {
                Application.Current.Shutdown(); return;
            }

            var progressWindow = new SetupProgressWindow();
            progressWindow.Show();

            try
            {
                setup.OnProgress = (pct, msg) =>
                    Dispatcher.Invoke(() => progressWindow.Update(pct, msg));

                await setup.EnsureMySQLAsync(password);
                _db.EnsureTableExists();

                progressWindow.Close();

                string lanIp = setup.GetLocalIP();
                MessageBox.Show(
                    $"MySQL installed and ready!\n\n" +
                    $"Other devices can connect using:\n" +
                    $"  Host : {lanIp}\n" +
                    $"  Port : 3306\n" +
                    $"  User : root\n" +
                    $"  DB   : WpfCrudDB",
                    "Setup Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                this.Show();
                LoadData();
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

        // ── Password Prompt ──────────────────────────────────────────────────

        private string PromptForPassword()
        {
            // Replace this with your own PasswordInputDialog if you have one
            // Or hardcode for dev: return "MyPass123!";
            var dlg = new PasswordInputDialog();
            dlg.ShowDialog();
            return dlg.Confirmed ? dlg.EnteredPassword : string.Empty;
        }

        // ── CRUD (same as before) ────────────────────────────────────────────

        void LoadData() => dgStudents.ItemsSource = _db.GetAllStudents();

        void ClearFields()
        {
            txtName.Clear(); txtAge.Clear(); txtEmail.Clear();
            _selectedId = -1;
        }

        bool TryGetInputs(out Student s)
        {
            s = new Student();
            if (string.IsNullOrWhiteSpace(txtName.Text) ||
                string.IsNullOrWhiteSpace(txtAge.Text) ||
                string.IsNullOrWhiteSpace(txtEmail.Text))
            {
                MessageBox.Show("Please fill in all fields.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            if (!int.TryParse(txtAge.Text, out int age) || age <= 0)
            {
                MessageBox.Show("Age must be a positive number.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            s = new Student
            {
                Id = _selectedId,
                Name = txtName.Text.Trim(),
                Age = age,
                Email = txtEmail.Text.Trim()
            };
            return true;
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetInputs(out var s)) return;
            if (_db.InsertStudent(s)) { ClearFields(); LoadData(); }
        }

        private void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedId == -1)
            { MessageBox.Show("Select a row first."); return; }
            if (!TryGetInputs(out var s)) return;
            if (_db.UpdateStudent(s)) { ClearFields(); LoadData(); }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedId == -1)
            { MessageBox.Show("Select a row first."); return; }
            var r = MessageBox.Show("Delete this student?", "Confirm",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r == MessageBoxResult.Yes && _db.DeleteStudent(_selectedId))
            { ClearFields(); LoadData(); }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        { ClearFields(); LoadData(); }

        private void DgStudents_SelectionChanged(object sender,
            System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (dgStudents.SelectedItem is Student s)
            {
                _selectedId = s.Id;
                txtName.Text = s.Name;
                txtAge.Text = s.Age.ToString();
                txtEmail.Text = s.Email;
            }
        }
    }
}