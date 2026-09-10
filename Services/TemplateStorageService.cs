using MyWPFCRUDApp.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MyWPFCRUDApp.Services
{
    // Persists user-designed label templates as one JSON file per template.
    //
    // Stored under Documents\MyWPFCRUDApp\LabelTemplates rather than
    // %AppData% — that folder is *actual* per-user application data, but
    // several common "PC cleaner" tools (CCleaner, Advanced SystemCare, etc.)
    // scan AppData for folders belonging to apps they don't see a proper
    // Windows installer/uninstall entry for, and delete them as "orphaned
    // leftovers" even though they aren't temp or cache data. Documents is
    // treated as user-created content by every cleaner of this kind, so it
    // isn't swept up the same way.
    //
    // The built-in Standard/Compact/Detailed templates stay hardcoded in
    // BarcodeLabelsWindow and never touch this folder.
    public static class TemplateStorageService
    {
        private static readonly string FolderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "MyWPFCRUDApp", "LabelTemplates");

        // Where templates used to live. Kept only so existing users don't
        // lose templates created before this change — see MigrateFromLegacyFolderIfNeeded.
        private static readonly string LegacyFolderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MyWPFCRUDApp", "LabelTemplates");

        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        public static List<LabelTemplate> LoadAll()
        {
            Directory.CreateDirectory(FolderPath);
            MigrateFromLegacyFolderIfNeeded();

            var templates = new List<LabelTemplate>();

            foreach (var file in Directory.GetFiles(FolderPath, "*.json"))
            {
                try
                {
                    var tpl = JsonSerializer.Deserialize<LabelTemplate>(File.ReadAllText(file));
                    if (tpl != null) templates.Add(tpl);
                }
                catch
                {
                    // Skip a corrupt/hand-edited file rather than crash the window.
                }
            }
            return templates.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static void Save(LabelTemplate template)
        {
            Directory.CreateDirectory(FolderPath);

            // Write to a temp file first, then swap it into place. If the app
            // is killed mid-write, the half-written data lands in the .tmp
            // file, not the real one — the previously saved template (or
            // nothing, for a first save) is what survives, never a corrupt
            // truncated file that LoadAll() would silently drop.
            var finalPath = GetPath(template.Name);
            var tempPath = finalPath + ".tmp";

            File.WriteAllText(tempPath, JsonSerializer.Serialize(template, JsonOptions));
            File.Copy(tempPath, finalPath, overwrite: true);
            File.Delete(tempPath);
        }

        public static void Delete(LabelTemplate template)
        {
            var path = GetPath(template.Name);
            if (File.Exists(path)) File.Delete(path);
        }

        private static string GetPath(string name)
        {
            var safe = string.Concat(name.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(FolderPath, safe + ".json");
        }

        // One-time move: if the new Documents folder is empty but the old
        // AppData folder has templates in it (i.e. this user upgraded from a
        // version that saved there), copy them across so nothing is lost.
        // Safe to call every time LoadAll() runs — it's a no-op once the new
        // folder has anything in it.
        private static void MigrateFromLegacyFolderIfNeeded()
        {
            try
            {
                if (!Directory.Exists(LegacyFolderPath)) return;
                if (Directory.GetFiles(FolderPath, "*.json").Length > 0) return; // already migrated / already has data

                foreach (var file in Directory.GetFiles(LegacyFolderPath, "*.json"))
                {
                    var dest = Path.Combine(FolderPath, Path.GetFileName(file));
                    if (!File.Exists(dest))
                        File.Copy(file, dest);
                }
            }
            catch
            {
                // Migration is best-effort; a failure here shouldn't block
                // the window from opening with whatever templates it can find.
            }
        }
    }
}