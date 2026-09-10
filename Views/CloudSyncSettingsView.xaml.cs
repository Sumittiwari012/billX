using System;
using System.Windows;
using System.Windows.Controls;
using MyWPFCRUDApp.Services;
using MySql.Data.MySqlClient;

namespace MyWPFCRUDApp.Views
{
    public partial class CloudSyncSettingsView : UserControl
    {
        public CloudSyncSettingsView()
        {
            InitializeComponent();
            PrefillFromSavedSettings();
        }

        /// <summary>
        /// Pre-fills the visible fields (never the password) from whatever is
        /// already saved, so re-opening this screen doesn't look empty.
        /// </summary>
        private void PrefillFromSavedSettings()
        {
            var saved = CloudSettingsService.LoadConnectionString();
            if (string.IsNullOrEmpty(saved))
                return;

            try
            {
                var builder = new MySqlConnectionStringBuilder(saved);
                TxtServer.Text = builder.Server;
                TxtPort.Text = builder.Port.ToString();
                TxtDatabase.Text = builder.Database;
                TxtUser.Text = builder.UserID;
                // Password intentionally left blank - it is never redisplayed.
                StatusText.Text = "Loaded saved connection settings (re-enter password to change/sync).";
            }
            catch
            {
                // Ignore - if the saved string is somehow malformed, just leave fields blank.
            }
        }

        private string BuildConnectionString()
        {
            var builder = new MySqlConnectionStringBuilder
            {
                Server = TxtServer.Text.Trim(),
                Port = uint.TryParse(TxtPort.Text.Trim(), out var port) ? port : 3306u,
                Database = TxtDatabase.Text.Trim(),
                UserID = TxtUser.Text.Trim(),
                Password = PwdPassword.Password,
                SslMode = MySqlSslMode.Prefered
            };
            return builder.ConnectionString;
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TxtServer.Text) ||
                    string.IsNullOrWhiteSpace(TxtDatabase.Text) ||
                    string.IsNullOrWhiteSpace(TxtUser.Text) ||
                    string.IsNullOrWhiteSpace(PwdPassword.Password))
                {
                    StatusText.Text = "Please fill in server, database, username and password.";
                    return;
                }

                var connStr = BuildConnectionString();
                CloudSettingsService.SaveConnectionString(connStr);
                CloudSyncService.CloudConnectionString = connStr;

                StatusText.Text = "Cloud connection settings saved securely.";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Failed to save settings: {ex.Message}";
            }
        }

        /// <summary>
        /// Single-button sync: pulls whatever's new from the cloud first (additive,
        /// non-destructive to local data), then pushes the resulting local state up
        /// to the cloud (which replaces the cloud's data entirely). If the pull
        /// fails, the push is skipped - no point pushing before we know local is
        /// caught up. If the pull succeeds but the push fails, local already has
        /// the pulled data (that part committed on its own), only the push is
        /// rolled back.
        /// </summary>
        private async void SyncWithCloud_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(CloudSyncService.CloudConnectionString))
            {
                MessageBox.Show(
                    "Save your cloud connection settings first.",
                    "Missing Settings",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                "This will first pull any new customers, purchases, payments, and " +
                "product quantities from the cloud into your local database " +
                "(existing local data is not changed or removed), and then push the " +
                "resulting local data up to the cloud, permanently erasing whatever " +
                "is currently in the cloud database and replacing it. This cannot be " +
                "undone. Continue?",
                "Confirm Sync With Cloud",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            BtnSyncWithCloud.IsEnabled = false;
            BtnSaveSettings.IsEnabled = false;

            // Declared as IProgress<string> (not var/Progress<string>) so .Report(...)
            // is visible below - Progress<T> only implements Report via the explicit
            // interface IProgress<T>.Report, it's not a public instance method.
            IProgress<string> progress = new Progress<string>(msg => StatusText.Text = msg);

            try
            {
                progress.Report("Starting sync: pulling from cloud...");
                await CloudPullService.PullCustomerDataFromCloudAsync(progress);

                progress.Report("Pull complete. Pushing local data to cloud...");
                await CloudSyncService.SyncLocalToCloudAsync(progress);

                MessageBox.Show(
                    "Sync completed successfully: pulled new data from the cloud, " +
                    "then pushed the local database up to the cloud.",
                    "Done",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Sync failed and was rolled back:\n{ex.Message}",
                    "Sync Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                BtnSyncWithCloud.IsEnabled = true;
                BtnSaveSettings.IsEnabled = true;
            }
        }
    }
}