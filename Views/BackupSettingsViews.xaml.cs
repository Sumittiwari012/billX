using MyWPFCRUDApp.Models;
using MyWPFCRUDApp.Services;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MyWPFCRUDApp.Views
{
    public partial class BackupSettingsViews : UserControl
    {
        public BackupSettingsViews()
        {
            InitializeComponent();
            LoadIntoForm(BackupSettingsService.Load());
            UpdateDetectedHostDisplay();
        }

        private void LoadIntoForm(BackupSettings s)
        {
            TxtBackupEmail.Text = s.BackupEmail;
            TxtBackupFolder.Text = s.BackupFolderPath;
            ChkEnableBackupOnClose.IsChecked = s.EnableBackupOnClose;
            TxtSmtpPort.Text = s.SmtpPort.ToString();
            ChkSmtpUseSsl.IsChecked = s.SmtpUseSsl;
            TxtSmtpUsername.Text = s.SmtpUsername;
            PwdSmtpPassword.Password = s.SmtpPassword;
            TxtSenderDisplayName.Text = s.SenderDisplayName;
        }

        private BackupSettings ReadFromForm()
        {
            int.TryParse(TxtSmtpPort.Text, out int port);
            string username = TxtSmtpUsername.Text.Trim();

            return new BackupSettings
            {
                BackupEmail = TxtBackupEmail.Text.Trim(),
                BackupFolderPath = TxtBackupFolder.Text.Trim(),
                // No UI for this anymore — blank means "rely on mysqldump
                // being available on PATH", which DatabaseBackupService
                // already handles.
                MySqlDumpExePath = string.Empty,
                EnableBackupOnClose = ChkEnableBackupOnClose.IsChecked == true,
                // Derived from the SMTP username's domain instead of a
                // manual field — see SmtpHostLookup.
                SmtpHost = SmtpHostLookup.Detect(username),
                SmtpPort = port == 0 ? 587 : port,
                SmtpUseSsl = ChkSmtpUseSsl.IsChecked == true,
                SmtpUsername = username,
                SmtpPassword = PwdSmtpPassword.Password,
                SenderDisplayName = string.IsNullOrWhiteSpace(TxtSenderDisplayName.Text)
                    ? "Store Backup"
                    : TxtSenderDisplayName.Text.Trim(),
            };
        }

        // Keeps the little "Server: smtp.gmail.com" line under the SMTP
        // username field in sync as the user types, so the auto-detection
        // isn't a total black box even though there's no editable host field.
        private void TxtSmtpUsername_TextChanged(object sender, TextChangedEventArgs e) => UpdateDetectedHostDisplay();

        private void UpdateDetectedHostDisplay()
        {
            string host = SmtpHostLookup.Detect(TxtSmtpUsername.Text.Trim());
            TxtDetectedHostDisplay.Text = string.IsNullOrWhiteSpace(host)
                ? "Server will be detected once you enter the SMTP username above."
                : $"Server: {host}";
        }

        // Folder picker built on Microsoft.Win32.OpenFileDialog (already part
        // of WPF, no extra assembly reference needed) instead of
        // System.Windows.Forms.FolderBrowserDialog. The trick: point it at a
        // fake filename inside the folder the user wants, with file-existence
        // checks turned off, then take the directory of whatever they "pick".
        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Choose a backup folder",
                CheckFileExists = false,
                CheckPathExists = true,
                ValidateNames = false,
                FileName = "Select this folder"
            };

            if (!string.IsNullOrWhiteSpace(TxtBackupFolder.Text) && Directory.Exists(TxtBackupFolder.Text))
                dlg.InitialDirectory = TxtBackupFolder.Text;

            if (dlg.ShowDialog() == true)
            {
                string? folder = Path.GetDirectoryName(dlg.FileName);
                if (!string.IsNullOrWhiteSpace(folder))
                    TxtBackupFolder.Text = folder;
            }
        }

        // NOTE: MessageBox.Show's overload with an owner parameter only
        // accepts a Window, not a UserControl — this control lives inside
        // whatever Window hosts CurrentView, so it has no Window of its own
        // to pass in. The ownerless overload just centers on the screen
        // instead of the parent window, which is cosmetic only.
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var settings = ReadFromForm();

            if (settings.EnableBackupOnClose)
            {
                if (string.IsNullOrWhiteSpace(settings.BackupFolderPath))
                {
                    MessageBox.Show("Choose a backup folder before enabling backup on close.",
                        "Missing folder", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (string.IsNullOrWhiteSpace(settings.BackupEmail) || string.IsNullOrWhiteSpace(settings.SmtpHost))
                {
                    MessageBox.Show("Enter a backup email address and SMTP username before enabling backup on close.",
                        "Missing email settings", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            BackupSettingsService.Save(settings);
            MessageBox.Show("Backup settings saved.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BackupNowButton_Click(object sender, RoutedEventArgs e)
        {
            var settings = ReadFromForm();
            settings.EnableBackupOnClose = true; // force the create+email path to run for this manual test, regardless of the checkbox

            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                var result = DatabaseBackupService.RunCloseTimeBackup(settings);
                MessageBox.Show(result.Message, result.Success ? "Backup complete" : "Backup problem",
                    MessageBoxButton.OK, result.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void TestEmailButton_Click(object sender, RoutedEventArgs e)
        {
            var settings = ReadFromForm();

            // A quick SMTP round-trip with a tiny temp text file rather than a
            // full database dump, so mail credentials can be checked fast.
            string tempFile = Path.Combine(Path.GetTempPath(), "backup_test_email.txt");
            File.WriteAllText(tempFile, $"Test email from the Backup Settings screen, sent {DateTime.Now}.");

            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                var result = DatabaseBackupService.SendBackupEmail(settings, tempFile);
                MessageBox.Show(result.Message, result.Success ? "Email sent" : "Email problem",
                    MessageBoxButton.OK, result.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
                try { File.Delete(tempFile); } catch { /* best effort cleanup */ }
            }
        }
    }
}