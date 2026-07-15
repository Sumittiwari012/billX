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

        private async void PullFromCloud_Click(object sender, RoutedEventArgs e)
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
                "This will add any new customers, purchases, payments, and product " +
                "quantities from the cloud into your local database. Existing local " +
                "data is not changed or removed. Continue?",
                "Confirm Pull From Cloud",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            BtnPushToCloud.IsEnabled = false;
            BtnPullFromCloud.IsEnabled = false;
            BtnSaveSettings.IsEnabled = false;
            var progress = new Progress<string>(msg => StatusText.Text = msg);

            try
            {
                await CloudPullService.PullCustomerDataFromCloudAsync(progress);
                MessageBox.Show(
                    "Pull from cloud completed successfully.",
                    "Done",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Pull failed and was rolled back:\n{ex.Message}",
                    "Pull Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                BtnPushToCloud.IsEnabled = true;
                BtnPullFromCloud.IsEnabled = true;
                BtnSaveSettings.IsEnabled = true;
            }
        }

        private async void PushToCloud_Click(object sender, RoutedEventArgs e)
        {
            // Make sure we actually have a connection string to use - either just
            // saved in this session, or loaded at app startup from a previous save.
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
                "This will permanently erase all data currently in the cloud database " +
                "and replace it with your local data. This cannot be undone. Continue?",
                "Confirm Cloud Sync",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            BtnPushToCloud.IsEnabled = false;
            BtnSaveSettings.IsEnabled = false;
            var progress = new Progress<string>(msg => StatusText.Text = msg);

            try
            {
                await CloudSyncService.SyncLocalToCloudAsync(progress);
                MessageBox.Show(
                    "Cloud sync completed successfully.",
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
                BtnPushToCloud.IsEnabled = true;
                BtnSaveSettings.IsEnabled = true;
            }
        }
    }
}