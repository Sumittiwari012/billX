using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MyWPFCRUDApp.Services
{
    /// <summary>
    /// Persists the cloud MySQL connection string on disk, encrypted with Windows
    /// DPAPI so it can only be decrypted by the same Windows user account on the
    /// same machine. This is what lets CloudSyncSettingsView "remember" the saved
    /// server/database/user (and the password, internally) between app runs,
    /// without ever displaying the password back in the UI.
    /// </summary>
    public static class CloudSettingsService
    {
        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MyWPFCRUDApp",
            "cloudsettings.dat");

        /// <summary>
        /// Encrypts and saves the given connection string to disk.
        /// </summary>
        public static void SaveConnectionString(string connectionString)
        {
            if (connectionString == null)
                throw new ArgumentNullException(nameof(connectionString));

            var directory = Path.GetDirectoryName(SettingsFilePath)!;
            Directory.CreateDirectory(directory);

            var plainBytes = Encoding.UTF8.GetBytes(connectionString);
            var encryptedBytes = ProtectedData.Protect(
                plainBytes,
                optionalEntropy: null,
                scope: DataProtectionScope.CurrentUser);

            File.WriteAllBytes(SettingsFilePath, encryptedBytes);
        }

        /// <summary>
        /// Loads and decrypts the previously saved connection string.
        /// Returns null if nothing has been saved yet, or if the saved file can't
        /// be decrypted (e.g. it was copied from a different machine/user account).
        /// </summary>
        public static string? LoadConnectionString()
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                    return null;

                var encryptedBytes = File.ReadAllBytes(SettingsFilePath);
                var plainBytes = ProtectedData.Unprotect(
                    encryptedBytes,
                    optionalEntropy: null,
                    scope: DataProtectionScope.CurrentUser);

                return Encoding.UTF8.GetString(plainBytes);
            }
            catch
            {
                // Corrupt, missing, or undecryptable (different user/machine) -
                // treat as "nothing saved" rather than crashing the app.
                return null;
            }
        }

        /// <summary>
        /// Deletes any saved cloud connection settings.
        /// </summary>
        public static void ClearSavedConnectionString()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                    File.Delete(SettingsFilePath);
            }
            catch
            {
                // Ignore - not critical if cleanup fails.
            }
        }
    }
}