using System;
using System.IO;
using System.Linq;
using System.Printing;
using System.Text.Json;

namespace MyWPFCRUDApp.Helpers
{
    public static class PrinterSettingsService
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MyWPFCRUDApp", "printer-settings.json");

        private class PrinterSettingsData
        {
            public string DefaultPrinterName { get; set; }
        }

        // ── Save ────────────────────────────────────────────────────────────
        public static void SaveDefaultPrinter(string printerFullName)
        {
            var dir = Path.GetDirectoryName(SettingsPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var data = new PrinterSettingsData { DefaultPrinterName = printerFullName };
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(data));
        }

        // ── Load saved printer name (raw string, may be null) ──────────────
        public static string GetSavedPrinterName()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return null;
                var json = File.ReadAllText(SettingsPath);
                var data = JsonSerializer.Deserialize<PrinterSettingsData>(json);
                return data?.DefaultPrinterName;
            }
            catch
            {
                return null;
            }
        }

        // ── Resolve to an actual live PrintQueue, with fallback ────────────
        public static PrintQueue GetDefaultPrintQueue()
        {
            var server = new LocalPrintServer();
            var savedName = GetSavedPrinterName();

            if (!string.IsNullOrWhiteSpace(savedName))
            {
                var match = server.GetPrintQueues()
                    .FirstOrDefault(q => q.FullName == savedName);
                if (match != null) return match;
            }

            // Saved printer no longer exists / nothing saved yet — fall back
            // to whatever Windows currently considers the default.
            return server.DefaultPrintQueue;
        }
    }
}