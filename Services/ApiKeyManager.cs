using System;
using System.IO;

namespace MyWPFCRUDApp.Services
{
    /// <summary>
    /// Manages the Groq API key stored in AppData\Local\BillX\groq_key.txt
    /// Never hardcoded — always read from / written to disk.
    /// </summary>
    public static class ApiKeyManager
    {
        private static readonly string KeyFolder =
            Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData), "BillX");

        private static readonly string KeyFile =
            Path.Combine(KeyFolder, "groq_key.txt");

        /// <summary>Returns the saved key, or empty string if not set.</summary>
        public static string GetKey()
        {
            try
            {
                if (File.Exists(KeyFile))
                    return File.ReadAllText(KeyFile).Trim();
            }
            catch { }
            return string.Empty;
        }

        /// <summary>Saves the key to disk.</summary>
        public static void SaveKey(string key)
        {
            Directory.CreateDirectory(KeyFolder);
            File.WriteAllText(KeyFile, key.Trim());
        }

        /// <summary>Returns true if a non-empty key is saved.</summary>
        public static bool HasKey() => !string.IsNullOrWhiteSpace(GetKey());

        /// <summary>Deletes the saved key.</summary>
        public static void ClearKey()
        {
            if (File.Exists(KeyFile))
                File.Delete(KeyFile);
        }
    }
}
