using System;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using System.Text.RegularExpressions;
namespace SaveManagement
{
	public static class SaveUtility
	{
		public static readonly string extension = ".pc";

		public static int key = 129;

		public static void Save(string path, string data)
		{
			File.WriteAllText(path, data);
		}

		public static string Load(string path)
		{
			return File.ReadAllText(path);
		}

		public static string EncryptDecrypt(string textToEncrypt)
		{
			char[] chr = textToEncrypt.ToCharArray();
			char[] dat = new char[chr.Length];
			for (int i = 0; i < chr.Length; i++)
			{
				dat[i] = (char)(chr[i] ^ key);
			}
			return new string(dat);
		}

		public static string GetNewPath(string name)
		{
			string folder = GetFolderPath();
			string baseName = name;
			int currentIndex = 0;
			var regex = new Regex(@"^(.*) \((\d+)\)$");
			var match = regex.Match(name);
			if (match.Success)
			{
				baseName = match.Groups[1].Value;
				currentIndex = int.Parse(match.Groups[2].Value);
			}
			string path = Path.Combine(folder, name + extension);
			if (!File.Exists(path))
			{
				return path;
			}
			int id = currentIndex + 1;
			string newName;
			string newPath;
			do
			{
				newName = $"{baseName} ({id})";
				newPath = Path.Combine(folder, newName + extension);
				id++;
			} while (File.Exists(newPath));
			return newPath;
		}

		public static string GetFolderPath()
		{
			string p = Application.persistentDataPath + "/saves/";
			if (!Directory.Exists(p))
			{
				Directory.CreateDirectory(p);
			}
			return p;
		}

		public static string GetTextFromStreamingAssets(string relativePath)
		{
			string fullPath = Path.Combine(Application.streamingAssetsPath, relativePath);
			string result = null;
			Exception exception = null;

			ManualResetEvent doneEvent = new ManualResetEvent(false);

			UnityWebRequest request = UnityWebRequest.Get(fullPath);
			var operation = request.SendWebRequest();

			operation.completed += _ =>
			{
				if (request.result == UnityWebRequest.Result.ConnectionError ||
					request.result == UnityWebRequest.Result.ProtocolError)
				{
					exception = new Exception(request.error);
				}
				else
				{
					result = request.downloadHandler.text;
				}

				doneEvent.Set();
			};

			doneEvent.WaitOne();

			if (exception != null)
			{
				return null;
			}

			return result;
		}
	}
}
