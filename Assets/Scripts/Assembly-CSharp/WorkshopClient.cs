using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using SaveManagement;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class WorkshopItem
{
	public int id;
	public string title;
	public string author;
	public string description;
	public int size_bytes;
	public string created_at;
	public int downloads;
}

[Serializable]
public class WorkshopListResponse
{
	public bool ok;
	public string error;
	public WorkshopItem[] items;
}

[Serializable]
public class WorkshopUploadResponse
{
	public bool ok;
	public string error;
	public int id;
}

public class WorkshopClient : MonoBehaviour
{
	public static string ApiUrl = "https://orangepcsimu.byethost4.com/workshop/api.php";
	public static string UploadKey = "";
	private static string byetCookie;

	public static WorkshopClient Instance { get; private set; }

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Destroy(gameObject);
			return;
		}
		Instance = this;
		DontDestroyOnLoad(gameObject);
	}

	public void ListSaves(Action<List<WorkshopItem>, string> done)
	{
		StartCoroutine(GetList(done));
	}

	private IEnumerator GetList(Action<List<WorkshopItem>, string> done)
	{
		yield return BypassByethost();
		using (var req = UnityWebRequest.Get(ApiUrl + "?action=list&i=1"))
		{
			ApplyCookie(req);
			req.timeout = 20;
			yield return req.SendWebRequest();
			if (req.result != UnityWebRequest.Result.Success)
			{
				done(null, req.error);
				yield break;
			}
			var json = StripJunk(req.downloadHandler.text);
			WorkshopListResponse parsed = null;
			try { parsed = JsonUtility.FromJson<WorkshopListResponse>(json); }
			catch (Exception e)
			{
				done(null, e.Message);
				yield break;
			}
			if (parsed == null || !parsed.ok)
			{
				done(null, parsed != null ? parsed.error : "bad json");
				yield break;
			}
			done(parsed.items != null ? new List<WorkshopItem>(parsed.items) : new List<WorkshopItem>(), null);
		}
	}

	public void Download(WorkshopItem item, Action<string, string> done)
	{
		StartCoroutine(DownloadCo(item, done));
	}

	private IEnumerator DownloadCo(WorkshopItem item, Action<string, string> done)
	{
		yield return BypassByethost();
		using (var req = UnityWebRequest.Get(ApiUrl + "?action=download&id=" + item.id + "&i=1"))
		{
			ApplyCookie(req);
			req.timeout = 60;
			yield return req.SendWebRequest();
			if (req.result != UnityWebRequest.Result.Success)
			{
				done(null, req.error);
				yield break;
			}
			var bytes = req.downloadHandler.data;
			if (bytes == null || bytes.Length < 8)
			{
				done(null, "empty file");
				yield break;
			}
			string title = string.IsNullOrEmpty(item.title) ? "workshop" : item.title;
			title = SceneSettings.CheckName(title);
			string path = SaveUtility.GetNewPath(title);
			try
			{
				File.WriteAllBytes(path, bytes);
			}
			catch (Exception e)
			{
				done(null, e.Message);
				yield break;
			}
			done(path, null);
		}
	}

	public void Upload(string localPath, string title, string author, string description, Action<int, string> done)
	{
		StartCoroutine(UploadCo(localPath, title, author, description, done));
	}

	private IEnumerator UploadCo(string localPath, string title, string author, string description, Action<int, string> done)
	{
		if (!File.Exists(localPath))
		{
			done(0, "no file");
			yield break;
		}
		var bytes = File.ReadAllBytes(localPath);
		if (bytes.Length > 1048576)
		{
			done(0, "file > 1MB");
			yield break;
		}

		var form = new List<IMultipartFormSection>
		{
			new MultipartFormFileSection("file", bytes, Path.GetFileName(localPath), "application/octet-stream"),
			new MultipartFormDataSection("title", title ?? ""),
			new MultipartFormDataSection("author", author ?? "Player"),
			new MultipartFormDataSection("description", description ?? "")
		};
		if (!string.IsNullOrEmpty(UploadKey))
			form.Add(new MultipartFormDataSection("key", UploadKey));

		using (var req = UnityWebRequest.Post(ApiUrl + "?action=upload", form))
		{
			req.timeout = 60;
			yield return req.SendWebRequest();
			if (req.result != UnityWebRequest.Result.Success)
			{
				done(0, req.error);
				yield break;
			}
			var json = StripJunk(req.downloadHandler.text);
			WorkshopUploadResponse parsed = null;
			try { parsed = JsonUtility.FromJson<WorkshopUploadResponse>(json); }
			catch (Exception e)
			{
				done(0, e.Message);
				yield break;
			}
			if (parsed == null || !parsed.ok)
			{
				done(0, parsed != null ? parsed.error : "upload fail");
				yield break;
			}
			done(parsed.id, null);
		}
	}

	private static string StripJunk(string raw)
	{
		if (string.IsNullOrEmpty(raw)) return "{}";
		int a = raw.IndexOf('{');
		int b = raw.LastIndexOf('}');
		if (a >= 0 && b > a) return raw.Substring(a, b - a + 1);
		return raw;
	}

	private static void ApplyCookie(UnityWebRequest req)
	{
		if (!string.IsNullOrEmpty(byetCookie))
			req.SetRequestHeader("Cookie", byetCookie);
	}

	private IEnumerator BypassByethost()
	{
		if (!string.IsNullOrEmpty(byetCookie)) yield break;
		using (var req = UnityWebRequest.Get(ApiUrl + "?action=list"))
		{
			req.timeout = 15;
			yield return req.SendWebRequest();
			var html = req.downloadHandler != null ? req.downloadHandler.text : "";
			if (string.IsNullOrEmpty(html) || html.IndexOf("toNumbers") < 0)
			{
				if (!string.IsNullOrEmpty(html) && html.IndexOf('{') >= 0)
					yield break;
				yield break;
			}
			var ms = Regex.Matches(html, "toNumbers\\(\"([0-9a-fA-F]+)\"\\)");
			if (ms.Count < 3) yield break;
			try
			{
				byte[] key = Hex(ms[0].Groups[1].Value);
				byte[] iv = Hex(ms[1].Groups[1].Value);
				byte[] ct = Hex(ms[2].Groups[1].Value);
				using (var aes = Aes.Create())
				{
					aes.Mode = CipherMode.CBC;
					aes.Padding = PaddingMode.None;
					aes.Key = key;
					aes.IV = iv;
					var dec = aes.CreateDecryptor().TransformFinalBlock(ct, 0, ct.Length);
					byetCookie = "__test=" + BitConverter.ToString(dec).Replace("-", "").ToLowerInvariant();
				}
			}
			catch (Exception e)
			{
				Debug.LogWarning("[Workshop] byethost cookie: " + e.Message);
			}
		}
	}

	private static byte[] Hex(string s)
	{
		var b = new byte[s.Length / 2];
		for (int i = 0; i < b.Length; i++)
			b[i] = Convert.ToByte(s.Substring(i * 2, 2), 16);
		return b;
	}
}
