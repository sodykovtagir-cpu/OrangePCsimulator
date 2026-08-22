using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
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
	public int likes;
	public bool has_cover;
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
	public string owner_key;
	public int likes;
}

public class WorkshopClient : MonoBehaviour
{
	public static readonly string[] ApiUrls =
	{
		"https://orangepcsimu.byethost4.com/workshop/api.php"
	};

	public static string UploadKey = "";
	private static string byetCookie;
	private static string workingUrl;

	public static WorkshopClient Instance { get; private set; }

	private class AcceptAllCerts : CertificateHandler
	{
		protected override bool ValidateCertificate(byte[] certificateData) { return true; }
	}

	private static UnityWebRequestAsyncOperation SafeSend(UnityWebRequest req)
	{
		try { return req.SendWebRequest(); }
		catch (InvalidOperationException e)
		{
			Debug.LogWarning("[Workshop] " + e.Message);
			return null;
		}
	}

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
		string body = null;
		string err = null;
		yield return RequestGet("?action=list&i=1", (t, e) => { body = t; err = e; });
		if (err != null) { done(null, err); yield break; }
		var json = StripJunk(body);
		WorkshopListResponse parsed = null;
		try { parsed = JsonUtility.FromJson<WorkshopListResponse>(json); }
		catch (Exception e) { done(null, e.Message); yield break; }
		if (parsed == null || !parsed.ok)
		{
			done(null, parsed != null ? parsed.error : "bad json");
			yield break;
		}
		done(parsed.items != null ? new List<WorkshopItem>(parsed.items) : new List<WorkshopItem>(), null);
	}

	public void Download(WorkshopItem item, Action<string, string> done)
	{
		StartCoroutine(DownloadCo(item, done));
	}

	private IEnumerator DownloadCo(WorkshopItem item, Action<string, string> done)
	{
		byte[] bytes = null;
		string err = null;
		yield return RequestBytes("?action=download&id=" + item.id + "&i=1", (b, e) => { bytes = b; err = e; });
		if (err != null) { done(null, err); yield break; }
		if (bytes == null || bytes.Length < 8) { done(null, "empty file"); yield break; }
		string title = string.IsNullOrEmpty(item.title) ? "workshop" : item.title;
		title = SceneSettings.CheckName(title);
		string path = SaveUtility.GetNewPath(title);
		try { File.WriteAllBytes(path, bytes); }
		catch (Exception e) { done(null, e.Message); yield break; }
		try
		{
			var loader = new DataLoader(path);
			loader.LoadFromPath();
			if (loader.GameData != null)
			{
				loader.GameData.roomName = string.IsNullOrEmpty(item.title) ? loader.GameData.roomName : item.title;
				// Ключ владельца не передаётся скачанному файлу, а workshopSourceId
				// помечает сейв как «скачанный из мастерской» — перевыложить его
				// (защита от копирования) нельзя.
				loader.GameData.workshopKey = "";
				loader.GameData.workshopSourceId = item.id;
				loader.WriteToFile();
			}
		}
		catch { }
		done(path, null);
	}

	public static string ClientId()
	{
		var id = PlayerPrefs.GetString("WorkshopClientId", "");
		if (string.IsNullOrEmpty(id))
		{
			id = Guid.NewGuid().ToString("N");
			PlayerPrefs.SetString("WorkshopClientId", id);
			PlayerPrefs.Save();
		}
		return id;
	}

	public static string CoverUrl(int id)
	{
		var baseUrl = !string.IsNullOrEmpty(workingUrl) ? workingUrl : ApiUrls[0];
		return baseUrl + "?action=cover&id=" + id + "&i=1";
	}

	public void DownloadCover(int id, Action<Texture2D, string> done)
	{
		StartCoroutine(DownloadCoverCo(id, done));
	}

	private IEnumerator DownloadCoverCo(int id, Action<Texture2D, string> done)
	{
		byte[] bytes = null;
		string err = null;
		yield return RequestBytes("?action=cover&id=" + id + "&i=1", (b, e) => { bytes = b; err = e; });
		if (err != null) { done(null, err); yield break; }
		if (bytes == null || bytes.Length < 16) { done(null, "no cover"); yield break; }
		var tex = new Texture2D(2, 2);
		if (!tex.LoadImage(bytes)) { done(null, "bad cover"); yield break; }
		done(tex, null);
	}

	public void Upload(string localPath, string title, string author, string description, byte[] coverJpg, Action<int, string, string> done)
	{
		StartCoroutine(PostSave("upload", 0, null, localPath, title, author, description, coverJpg, done));
	}

	public void UpdateSave(int id, string ownerKey, string localPath, string title, string author, string description, byte[] coverJpg, Action<int, string, string> done)
	{
		StartCoroutine(PostSave("update", id, ownerKey, localPath, title, author, description, coverJpg, done));
	}

	public void DeleteSave(int id, string ownerKey, Action<string> done)
	{
		StartCoroutine(SimplePost("?action=delete&i=1", new List<IMultipartFormSection>
		{
			new MultipartFormDataSection("id", id.ToString()),
			new MultipartFormDataSection("owner_key", ownerKey ?? "")
		}, (body, err) =>
		{
			if (err != null) { done(err); return; }
			WorkshopUploadResponse p = null;
			try { p = JsonUtility.FromJson<WorkshopUploadResponse>(StripJunk(body)); }
			catch (Exception e) { done(e.Message); return; }
			done(p != null && p.ok ? null : (p != null ? p.error : "delete fail"));
		}));
	}

	public void Like(int id, Action<int, string> done)
	{
		StartCoroutine(SimplePost("?action=like&i=1", new List<IMultipartFormSection>
		{
			new MultipartFormDataSection("id", id.ToString()),
			new MultipartFormDataSection("client", ClientId())
		}, (body, err) =>
		{
			if (err != null) { done(0, err); return; }
			WorkshopUploadResponse p = null;
			try { p = JsonUtility.FromJson<WorkshopUploadResponse>(StripJunk(body)); }
			catch (Exception e) { done(0, e.Message); return; }
			if (p == null || !p.ok) { done(0, p != null ? p.error : "like fail"); return; }
			done(p.likes, null);
		}));
	}

	private IEnumerator SimplePost(string query, List<IMultipartFormSection> form, Action<string, string> done)
	{
		string body = null, err = null;
		yield return RequestPost(query, form, (t, e) => { body = t; err = e; });
		done(body, err);
	}

	private IEnumerator PostSave(string action, int id, string ownerKey, string localPath, string title, string author, string description, byte[] coverJpg, Action<int, string, string> done)
	{
		if (action == "upload" && (string.IsNullOrEmpty(localPath) || !File.Exists(localPath)))
		{
			done(0, null, "no file");
			yield break;
		}

		var form = new List<IMultipartFormSection>
		{
			new MultipartFormDataSection("title", title ?? ""),
			new MultipartFormDataSection("author", author ?? "Player"),
			new MultipartFormDataSection("description", description ?? "")
		};
		if (id > 0)
		{
			form.Add(new MultipartFormDataSection("id", id.ToString()));
			form.Add(new MultipartFormDataSection("owner_key", ownerKey ?? ""));
		}
		if (!string.IsNullOrEmpty(localPath) && File.Exists(localPath))
		{
			var bytes = File.ReadAllBytes(localPath);
			if (bytes.Length > 1048576) { done(0, null, "file > 1MB"); yield break; }
			form.Add(new MultipartFormFileSection("file", bytes, Path.GetFileName(localPath), "application/octet-stream"));
		}
		if (coverJpg != null && coverJpg.Length > 0)
			form.Add(new MultipartFormFileSection("cover", coverJpg, "cover.jpg", "image/jpeg"));
		if (!string.IsNullOrEmpty(UploadKey))
			form.Add(new MultipartFormDataSection("key", UploadKey));

		string body = null, err = null;
		yield return RequestPost("?action=" + action + "&i=1", form, (t, e) => { body = t; err = e; });
		if (err != null) { done(0, null, err); yield break; }
		WorkshopUploadResponse parsed = null;
		try { parsed = JsonUtility.FromJson<WorkshopUploadResponse>(StripJunk(body)); }
		catch (Exception e) { done(0, null, e.Message); yield break; }
		if (parsed == null || !parsed.ok)
		{
			done(0, null, parsed != null ? parsed.error : "fail");
			yield break;
		}
		done(parsed.id, parsed.owner_key, null);
	}

	private IEnumerator RequestGet(string query, Action<string, string> done)
	{
		yield return BypassByethost();
		foreach (var baseUrl in UrlOrder())
		{
			using (var req = UnityWebRequest.Get(baseUrl + query))
			{
				ApplyCookie(req);
				req.timeout = 25;
				req.certificateHandler = new AcceptAllCerts();
				var op = SafeSend(req);
				if (op == null) continue;
				yield return op;
				if (req.result == UnityWebRequest.Result.Success && !string.IsNullOrEmpty(req.downloadHandler.text))
				{
					workingUrl = baseUrl;
					if (req.downloadHandler.text.IndexOf("toNumbers") >= 0)
					{
						byetCookie = null;
						yield return BypassByethost();
						continue;
					}
					done(req.downloadHandler.text, null);
					yield break;
				}
				Debug.LogWarning("[Workshop] " + baseUrl + " -> " + req.error);
			}
		}
		done(null, "Empty reply from server");
	}

	private IEnumerator RequestBytes(string query, Action<byte[], string> done)
	{
		yield return BypassByethost();
		foreach (var baseUrl in UrlOrder())
		{
			using (var req = UnityWebRequest.Get(baseUrl + query))
			{
				ApplyCookie(req);
				req.timeout = 60;
				req.certificateHandler = new AcceptAllCerts();
				var op = SafeSend(req);
				if (op == null) continue;
				yield return op;
				if (req.result == UnityWebRequest.Result.Success && req.downloadHandler.data != null && req.downloadHandler.data.Length > 0)
				{
					workingUrl = baseUrl;
					done(req.downloadHandler.data, null);
					yield break;
				}
			}
		}
		done(null, "Empty reply from server");
	}

	private IEnumerator RequestPost(string query, List<IMultipartFormSection> form, Action<string, string> done)
	{
		yield return BypassByethost();
		foreach (var baseUrl in UrlOrder())
		{
			using (var req = UnityWebRequest.Post(baseUrl + query, form))
			{
				ApplyCookie(req);
				req.timeout = 60;
				req.certificateHandler = new AcceptAllCerts();
				var op = SafeSend(req);
				if (op == null) continue;
				yield return op;
				if (req.result == UnityWebRequest.Result.Success && !string.IsNullOrEmpty(req.downloadHandler.text))
				{
					workingUrl = baseUrl;
					done(req.downloadHandler.text, null);
					yield break;
				}
				Debug.LogWarning("[Workshop] POST " + baseUrl + " -> " + req.error);
			}
		}
		done(null, "Empty reply from server");
	}

	private static IEnumerable<string> UrlOrder()
	{
		if (!string.IsNullOrEmpty(workingUrl))
			yield return workingUrl;
		foreach (var u in ApiUrls)
			if (u != workingUrl) yield return u;
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
		req.SetRequestHeader("User-Agent", "Mozilla/5.0 OrangePCSimulator");
	}

	private IEnumerator BypassByethost()
	{
		if (!string.IsNullOrEmpty(byetCookie)) yield break;
		foreach (var baseUrl in ApiUrls)
		{
			using (var req = UnityWebRequest.Get(baseUrl + "?action=list"))
			{
				req.timeout = 15;
				req.SetRequestHeader("User-Agent", "Mozilla/5.0 OrangePCSimulator");
				req.certificateHandler = new AcceptAllCerts();
				var op = SafeSend(req);
				if (op == null) continue;
				yield return op;
				var html = req.downloadHandler != null ? req.downloadHandler.text : "";
				if (string.IsNullOrEmpty(html)) continue;
				if (html.IndexOf('{') >= 0 && html.IndexOf("toNumbers") < 0)
				{
					workingUrl = baseUrl;
					yield break;
				}
				var ms = Regex.Matches(html, "toNumbers\\(\"([0-9a-fA-F]+)\"\\)");
				if (ms.Count < 3) continue;
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
						workingUrl = baseUrl;
						yield break;
					}
				}
				catch (Exception e)
				{
					Debug.LogWarning("[Workshop] cookie: " + e.Message);
				}
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
