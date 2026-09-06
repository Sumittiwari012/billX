using MyWPFCRUDApp.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MyWPFCRUDApp.Services
{
    // Persists user-designed label templates as one JSON file per template in
    // %AppData%\MyWPFCRUDApp\LabelTemplates. The built-in Standard/Compact/
    // Detailed templates stay hardcoded in BarcodeLabelsWindow and never
    // touch this folder.
    public static class TemplateStorageService
    {
        private static readonly string FolderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MyWPFCRUDApp", "LabelTemplates");

        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        public static List<LabelTemplate> LoadAll()
        {
            Directory.CreateDirectory(FolderPath);
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
            File.WriteAllText(GetPath(template.Name), JsonSerializer.Serialize(template, JsonOptions));
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
    }
}