using System;
using System.IO;
using UnityEngine;

namespace SaveManagement
{
    public class DataLoader
    {
        public GameData GameData { get; private set; }

        private string content = string.Empty;
        private bool headerOnly;
        public string Content
        {
            get
            {
                if (headerOnly)
                {
                    // Do not replace GameData: a menu may already have edited
                    // its name/workshop fields before writing the file.
                    string payload;
                    if (SaveUtility.Decode(File.ReadAllText(Path), out payload) ==
                        SaveUtility.SaveIntegrity.Tampered)
                        throw new Exception("Save integrity check failed (file may be modified).");
                    int split = payload.IndexOf('\n');
                    content = split < 0 ? string.Empty : payload.Substring(split + 1);
                    headerOnly = false;
                }
                return content;
            }
            set { content = value ?? string.Empty; headerOnly = false; }
        }

        public string Path { get; private set; }

        public DataLoader() { }

        public DataLoader(string path, GameData gameData)
        {
            Path = path;
            GameData = gameData;
        }

        public DataLoader(string path)
        {
            Path = path;
        }

        public void LoadFromPath()
        {
            if (string.IsNullOrEmpty(Path))
                throw new InvalidOperationException("Path is not set.");

            if (!File.Exists(Path))
                return;

            LoadFromString(File.ReadAllText(Path));
        }

        public void LoadHeaderFromPath()
        {
            if (string.IsNullOrEmpty(Path) || !File.Exists(Path))
                throw new FileNotFoundException("Save file is missing.", Path);
            string header;
            if (SaveUtility.DecodeHeader(File.ReadAllText(Path), out header) ==
                SaveUtility.SaveIntegrity.Tampered)
                throw new Exception("Save integrity check failed (file may be modified).");
            var parsed = JsonUtility.FromJson<GameData>(header);
            if (parsed == null) throw new Exception("Invalid GameData.");
            GameData = parsed;
            content = string.Empty;
            headerOnly = true;
        }

        public void LoadFromString(string data)
        {
            if (string.IsNullOrEmpty(data))
                throw new Exception("Save file is empty.");

            // OPC3: gzip + HMAC; OPC2: XOR + HMAC; legacy: XOR без подписи.
            string payload;
            var integrity = SaveUtility.Decode(data, out payload);
            if (integrity == SaveUtility.SaveIntegrity.Tampered)
                throw new Exception("Save integrity check failed (file may be modified).");

            string[] parts = payload.Split(new[] { '\n' }, 2);

            if (parts.Length < 1)
                throw new Exception("Invalid save format.");

            GameData = JsonUtility.FromJson<GameData>(parts[0]);

            if (GameData == null)
                throw new Exception("Invalid GameData.");

            Content = parts.Length > 1 ? parts[1] : string.Empty;
        }

        public void WriteToFile()
        {
            if (GameData == null)
                throw new InvalidOperationException("GameData is null.");

            string json = JsonUtility.ToJson(GameData);
            string content = Content ?? string.Empty;

            // Пишем сжатый OPC3; старые OPC2/.pc по-прежнему читаются.
            string encrypted = SaveUtility.Encode(json + "\n" + content);

            string directory = System.IO.Path.GetDirectoryName(Path);

            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(Path, encrypted);
        }
    }
}
