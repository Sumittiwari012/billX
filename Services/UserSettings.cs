using System;
using System.IO;
using System.Text.Json;

namespace MyWPFCRUDApp.Services
{
    /// <summary>
    /// Lightweight persistent settings stored in %AppData%\BillX\user_settings.json.
    /// Add any new user-preference properties here and they will survive app restarts.
    /// </summary>
    public class UserSettings
    {
        // ── Singleton ─────────────────────────────────────────────────────────
        private static UserSettings? _instance;
        public static UserSettings Instance => _instance ??= Load();

        // ── Settings file location ────────────────────────────────────────────
        private static string SettingsPath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BillX",
                "user_settings.json");

        // ── Persisted properties ──────────────────────────────────────────────
        /// <summary>Wholesale markup % used in the Bill Scan Review window.</summary>
        public decimal WholesalePercentage { get; set; } = 20m;

        /// <summary>MRP markup % used in the Bill Scan Review window.</summary>
        public decimal MRPPercentage { get; set; } = 40m;

        // ── Load ──────────────────────────────────────────────────────────────
        private static UserSettings Load()
        {
            try
            {
                string path = SettingsPath;
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var loaded = JsonSerializer.Deserialize<UserSettings>(json);
                    if (loaded != null) return loaded;
                }
            }
            catch { /* corrupt / missing — fall back to defaults */ }

            return new UserSettings();
        }

        // ── Save ──────────────────────────────────────────────────────────────
        public void Save()
        {
            try
            {
                string path = SettingsPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, JsonSerializer.Serialize(this,
                    new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { /* swallow — settings loss is non-fatal */ }
        }
    }
}
