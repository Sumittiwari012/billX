using MyWPFCRUDApp.Models;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MyWPFCRUDApp.Services
{
    // ════════════════════════════════════════════════════════════════════════
    // BackupSettingsService — load/save BackupSettings, same pattern as the
    // app's other *SettingsService classes (e.g. PrinterSettingsService):
    // a JSON file under %AppData%. The SMTP password is protected with
    // Windows DPAPI before it's written, so opening the JSON file directly
    // doesn't reveal the mail password in plain text.
    // ════════════════════════════════════════════════════════════════════════
    public static class BackupSettingsService
    {
        private static readonly string FolderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MyWPFCRUDApp");
        private static readonly string FilePath = Path.Combine(FolderPath, "backup_settings.json");

        public static BackupSettings Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new BackupSettings();

                var json = File.ReadAllText(FilePath);
                var settings = JsonSerializer.Deserialize<BackupSettings>(json) ?? new BackupSettings();
                settings.SmtpPassword = Decrypt(settings.EncryptedPasswordBase64);
                return settings;
            }
            catch
            {
                // Corrupt/unreadable settings file — fall back to defaults
                // rather than stop the Settings screen from opening at all.
                return new BackupSettings();
            }
        }

        public static void Save(BackupSettings settings)
        {
            settings.EncryptedPasswordBase64 = Encrypt(settings.SmtpPassword);

            Directory.CreateDirectory(FolderPath);
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }

        // DataProtectionScope.CurrentUser ties the encrypted value to the
        // Windows account that saved it — fine for a single-user store PC,
        // but note it means the same backup_settings.json won't decrypt
        // correctly if copied to a different Windows user/machine; the SMTP
        // password would just need re-entering in that case.
        private static string Encrypt(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return string.Empty;
            var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(bytes);
        }

        private static string Decrypt(string encryptedBase64)
        {
            if (string.IsNullOrEmpty(encryptedBase64)) return string.Empty;
            try
            {
                var bytes = ProtectedData.Unprotect(Convert.FromBase64String(encryptedBase64), null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                // Blob from a different Windows account, or corrupted —
                // return empty and let the user re-enter the password
                // instead of throwing during app startup.
                return string.Empty;
            }
        }
    }
}