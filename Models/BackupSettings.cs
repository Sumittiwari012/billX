using System.Text.Json.Serialization;

namespace MyWPFCRUDApp.Models
{
    // ════════════════════════════════════════════════════════════════════════
    // BackupSettings — everything the "Backup Settings" screen configures.
    // Persisted as JSON by BackupSettingsService; read by DatabaseBackupService
    // whenever a backup runs (both the manual "Backup Now" button and the
    // automatic one that fires when the app closes).
    // ════════════════════════════════════════════════════════════════════════
    public class BackupSettings
    {
        // Recipient of the backup email — the whole feature exists to land a
        // fresh copy of the database in this inbox every time the app closes.
        public string BackupEmail { get; set; } = string.Empty;

        // Local folder where the backup file lives. The SAME file name is
        // overwritten every close (see BackupFileName) — this is a "latest
        // backup" slot, not an accumulating history of timestamped files.
        public string BackupFolderPath { get; set; } = string.Empty;
        public string BackupFileName { get; set; } = "LatestBackup.sql";

        // Path to mysqldump.exe, e.g.
        // "C:\Program Files\MySQL\MySQL Server 8.0\bin\mysqldump.exe".
        // Leave blank to rely on "mysqldump" already being on PATH.
        public string MySqlDumpExePath { get; set; } = string.Empty;

        // ── Outgoing mail (SMTP) ─────────────────────────────────────────────
        public string SmtpHost { get; set; } = string.Empty;
        public int SmtpPort { get; set; } = 587;
        public bool SmtpUseSsl { get; set; } = true;
        public string SmtpUsername { get; set; } = string.Empty;
        public string SenderDisplayName { get; set; } = "Store Backup";

        // The plain SMTP password only ever lives in memory. What actually
        // gets written to backup_settings.json is EncryptedPasswordBase64
        // (see BackupSettingsService), so the password isn't sitting in a
        // plain-text file on disk.
        [JsonIgnore]
        public string SmtpPassword { get; set; } = string.Empty;
        public string EncryptedPasswordBase64 { get; set; } = string.Empty;

        // Master on/off switch — lets someone fill in all the details, try
        // "Backup Now" a few times, and only flip this on once they're happy,
        // without losing anything they've already typed.
        public bool EnableBackupOnClose { get; set; } = false;
    }
}