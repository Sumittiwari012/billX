using System;
using System.IO;
using System.Text.Json;

namespace MyWPFCRUDApp.Services
{
    // What gets remembered between runs for the barcode label print dialog.
    public class LabelPrintSettings
    {
        // How many labels sit side by side on one strip (typed by the user, 1-20).
        public int LabelsAcross { get; set; } = 1;

        // Empty space between neighbouring labels on the strip, in mm.
        public double GapMm { get; set; } = 2;

        // Last template chosen: "builtin:Standard" / "custom:My Template Name".
        public string? SelectedTemplateKey { get; set; }
    }

    // Stored under %APPDATA%\MyWPFCRUDApp (e.g. C:\Users\<you>\AppData\Roaming\MyWPFCRUDApp).
    // Clearing cache / temp files does NOT touch this folder, so settings survive.
    public static class LabelPrintSettingsService
    {
        private static readonly string Folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MyWPFCRUDApp");

        private static readonly string FilePath = Path.Combine(Folder, "label-print-settings.json");

        public static LabelPrintSettings Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new LabelPrintSettings();
                var settings = JsonSerializer.Deserialize<LabelPrintSettings>(File.ReadAllText(FilePath))
                               ?? new LabelPrintSettings();

                // Guard against hand-edited / corrupt values.
                settings.LabelsAcross = Math.Clamp(settings.LabelsAcross, 1, 20);
                settings.GapMm = Math.Clamp(settings.GapMm, 0, 50);
                return settings;
            }
            catch
            {
                // Unreadable file - fall back to defaults rather than blocking printing.
                return new LabelPrintSettings();
            }
        }

        public static void Save(LabelPrintSettings settings)
        {
            try
            {
                Directory.CreateDirectory(Folder);

                // Write to a temp file first, then swap it in, so a crash or
                // power cut mid-write can never leave a half-written settings file.
                var tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
                File.Move(tmp, FilePath, overwrite: true);
            }
            catch
            {
                // Failing to remember a preference must never break the print dialog.
            }
        }
    }
}