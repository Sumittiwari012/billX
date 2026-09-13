using MySql.Data.MySqlClient;
using System;
using System.Text.RegularExpressions;

namespace MyWPFCRUDApp.Services
{
    // ════════════════════════════════════════════════════════════════════════
    // BarcodeGenerator — produces the next sequential barcode by finding the
    // most recent product whose barcode follows the "letters + digits" auto-
    // increment shape (e.g. "M15", "GR1478") and incrementing its numeric
    // suffix by 1. Used by the "+ Add" quick-add button on the Purchase
    // screen, so a brand-new product can be dropped into an invoice with just
    // a name typed in, no barcode scan or manual entry required.
    //
    // FIX: previously this took MAX(CAST(Barcode AS UNSIGNED)) over every
    // PURELY NUMERIC barcode in the table and added 1 — which meant a real
    // scanned manufacturer/EAN barcode (e.g. "8904330603982", added via Scan
    // Bill or a barcode scanner) would get picked up as "the highest number"
    // and Quick Add would generate something like "8904330603983": a random-
    // looking, unrelated barcode instead of continuing the app's actual
    // "M15 / GR1478 / ..." sequence. This now mirrors the exact same pattern-
    // matching approach as ProductService.GetLastAutoBarcode() (already used
    // by Import Excel and Scan Bill), so all three entry points generate
    // barcodes that follow one consistent series.
    // ════════════════════════════════════════════════════════════════════════
    public static class BarcodeGenerator
    {
        // Used only when there are no existing "letters + digits" barcodes in
        // the database yet, so the very first auto-generated barcode still
        // has a sensible, readable starting point.
        private const string DefaultPrefix = "M";
        private const long DefaultStartingNumber = 1;

        public static string GetNext()
        {
            using var conn = new MySqlConnection(DatabaseHelper.ConnectionString);
            conn.Open();

            // Walk backward from the most recently created product and return
            // the first barcode that actually matches the "letters followed
            // by digits" auto-increment shape — skipping anything that
            // doesn't (purely numeric manufacturer/EAN codes, blank/odd
            // manual entries, etc.), same approach as
            // ProductService.GetLastAutoBarcode().
            using var cmd = new MySqlCommand(
                "SELECT Barcode FROM MProducts ORDER BY Id DESC", conn);
            using var reader = cmd.ExecuteReader();

            string? lastAutoBarcode = null;
            while (reader.Read())
            {
                string candidate = reader["Barcode"]?.ToString() ?? "";
                if (Regex.IsMatch(candidate, @"^[A-Za-z]+\d+$"))
                {
                    lastAutoBarcode = candidate;
                    break;
                }
            }

            if (lastAutoBarcode == null)
                return $"{DefaultPrefix}{DefaultStartingNumber}";

            var match = Regex.Match(lastAutoBarcode, @"^([A-Za-z]+)(\d+)$");
            string prefix = match.Groups[1].Value;
            long number = long.Parse(match.Groups[2].Value) + 1;

            return $"{prefix}{number}";
        }
    }
}