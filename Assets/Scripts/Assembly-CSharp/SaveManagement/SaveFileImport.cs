using System;
using System.IO;

namespace SaveManagement
{
    /// <summary>Single validated import path for picker and Android Open with.</summary>
    public static class SaveFileImport
    {
        public static string Import(string source, string displayName)
        {
            string name = Path.GetFileName(string.IsNullOrEmpty(displayName) ? source : displayName);
            string ext = Path.GetExtension(name).ToLowerInvariant();
            if (ext != ".pc" && ext != ".opc")
                throw new NotSupportedException("Unsupported save extension: " + ext);
            if (!File.Exists(source)) throw new FileNotFoundException("Save not found", source);

            var loader = new DataLoader(source);
            loader.LoadFromPath(); // validate contents/HMAC before adding to saved games
            string baseName = Path.GetFileNameWithoutExtension(name);
            if (string.IsNullOrEmpty(SaveUtility.GetSafeFileName(baseName))) baseName = "Imported save";
            string destination = SaveUtility.GetNewPath(baseName); // never overwrite an existing save

            if (ext == ".pc")
            {
                var converted = new DataLoader(destination, loader.GameData);
                converted.Content = loader.Content;
                converted.WriteToFile();
            }
            else
            {
                File.Copy(source, destination);
            }
            return destination;
        }
    }
}
