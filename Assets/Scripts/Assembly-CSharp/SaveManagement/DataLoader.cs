using System.IO;
using UnityEngine;
namespace SaveManagement
{
	public class DataLoader
	{
		public GameData GameData { get; private set; }

		public string Content { get; set; }

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
			if (!File.Exists(Path)) return;
			LoadFromString(File.ReadAllText(Path));
		}

		public void LoadFromString(string data)
		{
			string dec = SaveUtility.EncryptDecrypt(data);
			string[] dat = dec.Split('\n', 2);
			GameData = JsonUtility.FromJson<GameData>(dat[0]);
			if (GameData == null)
			{
				throw new System.Exception("Invalid file!");
			}
			Content = dat[1];
		}

		public void WriteToFile()
		{
			string gdat = JsonUtility.ToJson(GameData);
			string ctc = Content;
			string contents = SaveUtility.EncryptDecrypt(gdat + "\n" + ctc);
			File.WriteAllText(Path, contents);
		}
	}
}
