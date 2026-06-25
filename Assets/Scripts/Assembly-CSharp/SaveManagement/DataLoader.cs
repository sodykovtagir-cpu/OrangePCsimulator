using System;
using System.IO;
using UnityEngine;

namespace SaveManagement
{
    public class DataLoader
    {
        public GameData GameData { get; private set; }

        public string Content { get; set; } = string.Empty;

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

        public void LoadFromString(string data)
        {
            if (string.IsNullOrEmpty(data))
                throw new Exception("Save file is empty.");

            string decrypted = SaveUtility.EncryptDecrypt(data);

            string[] parts = decrypted.Split(new[] { '\n' }, 2);

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

            string encrypted =
                SaveUtility.EncryptDecrypt(json + "\n" + content);

            string directory = System.IO.Path.GetDirectoryName(Path);

            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(Path, encrypted);
        }
    }
}