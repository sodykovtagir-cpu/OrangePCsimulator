using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using System.Text.RegularExpressions;
namespace SaveManagement
{
	public static class SaveUtility
	{
		public static string extension = ".opc";

		/// <summary>
		/// Ключ XOR-обфускации сохранён для обратной совместимости со старыми сейвами.
		/// НЕ является защитой — только скрывает содержимое.
		/// </summary>
		public static int key = 129;

		/// <summary>
		/// Маркер нового формата. Когда расшифрованный файл начинается с него —
		/// значит, это сейв нового формата с HMAC-подписью.
		/// (JSON GameData в старых сейвах всегда начинается с '{', поэтому коллизии нет.)
		/// </summary>
		public const string Tag = "OPC2:";

		/// <summary>
		/// Секрет для HMAC-подписи содержимого. Зашит в бинарь, поэтому против грамотного
		/// реверса не защищает, но останавливает «ручное редактирование» файла
		/// (подделку монет/BTС/playtime) — ровно то, что нужно по минимуму.
		/// </summary>
		private const string HmacSecret = "OrangePCSim-SaveInt-2026::do-not-edit";

		private static byte[] HmacKey => Encoding.UTF8.GetBytes(HmacSecret);

		/// <summary>Результат проверки целостности.</summary>
		public enum SaveIntegrity
		{
			/// <summary>Старый формат без HMAC (обратная совместимость).</summary>
			None = 0,
			/// <summary>Новый формат, подпись совпадает.</summary>
			Ok = 1,
			/// <summary>Новый формат, подпись не совпала (файл изменён/повреждён).</summary>
			Tampered = 2
		}

		public static void Save(string path, string data)
		{
			File.WriteAllText(path, data);
		}

		public static string Load(string path)
		{
			return File.ReadAllText(path);
		}

		/// <summary>Чистая XOR-обфускация (используется и старыми сейвами).</summary>
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

		/// <summary>
		/// Кодирует полезную нагрузку (GameData + "\n" + content) в файл нового формата:
		/// XOR + HMAC-подпись содержимого в шапке.
		/// </summary>
		public static string Encode(string payload)
		{
			string hmac = ComputeHmac(payload);
			string inner = Tag + hmac + "\n" + payload;
			return EncryptDecrypt(inner);
		}

		/// <summary>
		/// Декодирует файл и возвращает полезную нагрузку. Для нового формата проверяет
		/// HMAC; для старого — просто возвращает расшифрованное содержимое.
		/// </summary>
		public static SaveIntegrity Decode(string fileText, out string payload)
		{
			string decrypted = EncryptDecrypt(fileText);

			if (decrypted.StartsWith(Tag))
			{
				int nl = decrypted.IndexOf('\n');
				if (nl < 0)
				{
					payload = decrypted;
					return SaveIntegrity.Tampered; // повреждённый заголовок
				}
				string hmac = decrypted.Substring(Tag.Length, nl - Tag.Length);
				payload = decrypted.Substring(nl + 1);
				string expected = ComputeHmac(payload);
				bool ok = FixedEquals(hmac, expected);
				return ok ? SaveIntegrity.Ok : SaveIntegrity.Tampered;
			}

			payload = decrypted;
			return SaveIntegrity.None; // старый формат, HMAC нет
		}

		/// <summary>Просто вернуть полезную нагрузку (шапка срезается), без строгой проверки.</summary>
		public static string ReadPayload(string fileText)
		{
			Decode(fileText, out var payload);
			return payload;
		}

		private static string ComputeHmac(string payload)
		{
			using (var hmac = new HMACSHA256(HmacKey))
			{
				byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
				return Convert.ToBase64String(hash);
			}
		}

		private static bool FixedEquals(string a, string b)
		{
			if (a == null || b == null || a.Length != b.Length) return false;
			int diff = 0;
			for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
			return diff == 0;
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
