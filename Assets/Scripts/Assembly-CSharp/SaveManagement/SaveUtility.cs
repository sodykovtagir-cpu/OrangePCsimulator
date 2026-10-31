using System;
using System.IO;
using System.IO.Compression;
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
		/// Маркер прежнего подписанного формата. Его по-прежнему читаем,
		/// а новые сейвы имеют ASCII-маркер OPC3 и gzip-содержимое.
		/// (JSON GameData в старых сейвах всегда начинается с '{', поэтому коллизии нет.)
		/// </summary>
		public const string Tag = "OPC2:";
        public const string CompressedTag = "OPC3:";
        private const int MaxDecodedBytes = 256 * 1024 * 1024;

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

        /// <summary>Compress and sign the complete save. OPC3 is ASCII text:
        /// XOR would turn every base64 character into a two-byte UTF-8 codepoint
        /// and undo the space savings. OPC2 and legacy XOR saves still load.</summary>
        public static string Encode(string payload)
        {
            byte[] plain = Encoding.UTF8.GetBytes(payload ?? string.Empty);
            string packed;
            using (var output = new MemoryStream())
            {
                using (var zip = new GZipStream(output, System.IO.Compression.CompressionLevel.Optimal, true))
                    zip.Write(plain, 0, plain.Length);
                packed = Convert.ToBase64String(output.ToArray());
            }
            return CompressedTag + ComputeHmac(packed) + "\n" + packed;
        }

        // Save list needs only GameData (first JSON line). Authenticate the
        // entire OPC3 payload, but stop inflating gzip after that line instead
        // of parsing every item's files/cable poses for every row in the menu.
        public static SaveIntegrity DecodeHeader(string fileText, out string header)
        {
            header = string.Empty;
            if (string.IsNullOrEmpty(fileText)) return SaveIntegrity.Tampered;
            if (!fileText.StartsWith(CompressedTag, StringComparison.Ordinal))
            {
                var status = Decode(fileText, out var oldPayload);
                if (status != SaveIntegrity.Tampered)
                {
                    int end = oldPayload.IndexOf('\n');
                    header = end < 0 ? oldPayload : oldPayload.Substring(0, end);
                }
                return status;
            }
            int nl = fileText.IndexOf('\n');
            if (nl != CompressedTag.Length + 44) return SaveIntegrity.Tampered;
            string body = fileText.Substring(nl + 1);
            if (!FixedEquals(fileText.Substring(CompressedTag.Length, 44), ComputeHmac(body)))
                return SaveIntegrity.Tampered;
            try
            {
                using (var input = new MemoryStream(Convert.FromBase64String(body)))
                using (var zip = new GZipStream(input, CompressionMode.Decompress))
                using (var reader = new StreamReader(zip, new UTF8Encoding(false, true)))
                {
                    header = reader.ReadLine();
                    if (string.IsNullOrEmpty(header) || header.Length > 8 * 1024 * 1024)
                        return SaveIntegrity.Tampered;
                }
                return SaveIntegrity.Ok;
            }
            catch (Exception)
            {
                header = string.Empty;
                return SaveIntegrity.Tampered;
            }
        }

		/// <summary>Decode OPC3 (gzip), OPC2 (uncompressed), and old unsigned XOR saves.</summary>
        public static SaveIntegrity Decode(string fileText, out string payload)
        {
            payload = string.Empty;
            if (string.IsNullOrEmpty(fileText)) return SaveIntegrity.Tampered;
            string decrypted = fileText.StartsWith(CompressedTag, StringComparison.Ordinal)
                ? fileText : EncryptDecrypt(fileText);
            bool compressed = decrypted.StartsWith(CompressedTag, StringComparison.Ordinal);
            if (compressed || decrypted.StartsWith(Tag, StringComparison.Ordinal))
            {
                string marker = compressed ? CompressedTag : Tag;
                int nl = decrypted.IndexOf('\n');
                if (nl < 0 || nl != marker.Length + 44) return SaveIntegrity.Tampered;
                string hmac = decrypted.Substring(marker.Length, nl - marker.Length);
                string body = decrypted.Substring(nl + 1);
                if (!FixedEquals(hmac, ComputeHmac(body))) return SaveIntegrity.Tampered;
                if (!compressed) { payload = body; return SaveIntegrity.Ok; }

                try
                {
                    byte[] bytes = Convert.FromBase64String(body);
                    using (var input = new MemoryStream(bytes))
                    using (var zip = new GZipStream(input, CompressionMode.Decompress))
                    using (var output = new MemoryStream())
                    {
                        var buffer = new byte[32768];
                        int count;
                        while ((count = zip.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            if (output.Length + count > MaxDecodedBytes)
                                return SaveIntegrity.Tampered;
                            output.Write(buffer, 0, count);
                        }
                        payload = new UTF8Encoding(false, true).GetString(output.ToArray());
                    }
                    return SaveIntegrity.Ok;
                }
                catch (Exception) // damaged gzip, bad base64, or invalid UTF-8
                {
                    payload = string.Empty;
                    return SaveIntegrity.Tampered;
                }
            }
            // Unknown OPC headers must not accidentally bypass signature checks.
            if (decrypted.StartsWith("OPC", StringComparison.Ordinal))
                return SaveIntegrity.Tampered;
            payload = decrypted;
            return SaveIntegrity.None;
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

		private static readonly Regex illegalName = new Regex(
			@"[<>:""/\\|?*\x00-\x1F]",
			RegexOptions.Compiled
		);

		public static string GetSafeFileName(string input)
		{
			if (string.IsNullOrEmpty(input)) return input;
			return illegalName.Replace(input, "");
		}

		public static string GetNewPath(string name)
		{
			name = GetSafeFileName(name);
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
