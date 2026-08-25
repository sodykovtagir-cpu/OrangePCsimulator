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

[Serializable]
public class WorkshopQuizResponse
{
	public bool ok;
	public bool show;
	public string link;
	public string title;
	public string body;
}

[Serializable]
public class WorkshopRedeemResponse
{
	public bool ok;
	public string error;
	public int cash;
	public float btc;
}

[Serializable]
public class AccountAuthResponse
{
	public bool ok;
	public string error;
	public bool pending;
	public bool sent;
	public string token;
	public string name;
	public string email;
	public bool verified;
	public bool tg_bonus;
	public bool granted;
	public float btc;
	public string tg;
	public string link;
}

[Serializable]
public class AccountSaveItem
{
	public int id;
	public string title;
	public string description;
	public int downloads;
	public int likes;
	public string owner_key;
	public bool has_cover;
	public string created_at;
}

[Serializable]
public class AccountMeResponse
{
	public bool ok;
	public string error;
	public string name;
	public string email;
	public string tg;
	public bool tg_bonus;
	public bool verified;
	public AccountSaveItem[] saves;
}

public class WorkshopClient : MonoBehaviour
{
	public static readonly string[] ApiUrls =
	{
		"https://orangepcsimu.byethost4.com/workshop/api.php"
	};

	public static readonly string[] AccountUrls =
	{
		"https://orangepcsimu.byethost4.com/workshop/account.php"
	};

	public static string UploadKey = "f52aa253f7ee050a6069d858473880982acb4c5de7a929e3";
	private static string byetCookie;
	private static string workingUrl;

	public static WorkshopClient Instance { get; private set; }

	/// <summary>Создаёт клиента, если его ещё нет на сцене (меню аккаунта, промокоды и т.п.).</summary>
	public static WorkshopClient Ensure()
	{
		if (Instance != null) return Instance;
		var go = new GameObject("WorkshopClient");
		return go.AddComponent<WorkshopClient>();
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

	/// <summary>
	/// Запрашивает отложенный квиз от админ-панели сайта (action=quiz).
	/// Сервер отдаёт квиз один раз и очищает его.
	/// </summary>
	public void GetQuiz(Action<WorkshopQuizResponse, string> done)
	{
		StartCoroutine(GetQuizCo(done));
	}

	private IEnumerator GetQuizCo(Action<WorkshopQuizResponse, string> done)
	{
		string body = null;
		string err = null;
		yield return RequestGet("?action=quiz&i=1", (t, e) => { body = t; err = e; });
		if (err != null) { done(null, err); yield break; }
		WorkshopQuizResponse parsed = null;
		try { parsed = JsonUtility.FromJson<WorkshopQuizResponse>(StripJunk(body)); }
		catch (Exception e) { done(null, e.Message); yield break; }
		done(parsed, null);
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
		var fields = new List<IMultipartFormSection>
		{
			new MultipartFormDataSection("id", id.ToString()),
			new MultipartFormDataSection("owner_key", ownerKey ?? "")
		};
		if (ServerAccounts.LoggedIn)
			fields.Add(new MultipartFormDataSection("token", ServerAccounts.Token ?? ""));
		StartCoroutine(SimplePost("?action=delete&i=1", fields, (body, err) =>
		{
			if (err != null) { done(err); return; }
			WorkshopUploadResponse p = null;
			try { p = JsonUtility.FromJson<WorkshopUploadResponse>(StripJunk(body)); }
			catch (Exception e) { done(e.Message); return; }
			done(p != null && p.ok ? null : (p != null ? p.error : "delete fail"));
		}));
	}

	public static readonly string[] AllowedHosts =
	{
		"orangepcsimu.byethost4.com",
		"byethost4.com",
		"*.byethost4.com"
	};

	/// <summary>
	/// Принимает сертификат ТОЛЬКО для хостов мастерской (byethost). Раньше принимал
	/// любой сертификат (MITM-риск). Сертификат byethost легитимен (ZeroSSL, SAN
	/// *.byethost4.com), поэтому обход нужен лишь на устройствах, где цепочка
	/// ZeroSSL не полностью доверена. Сужаем до whitelist, а не «accept all».
	/// </summary>
	private class AcceptAllCerts : CertificateHandler
	{
		protected override bool ValidateCertificate(byte[] certificateData)
		{
			try
			{
				if (certificateData == null || certificateData.Length == 0) return false;
				var cert = new System.Security.Cryptography.X509Certificates.X509Certificate2(certificateData);
				if (IsByethost(cert)) return true;
				return false;
			}
			catch
			{
				return false;
			}
		}

		private static bool IsByethost(System.Security.Cryptography.X509Certificates.X509Certificate2 cert)
		{
			// Primary: DNS (SAN) / CN. Falls back to Subject if GetNameInfo
			// is restricted on the current platform (it throws on some Unity targets).
			try
			{
				string dns = cert.GetNameInfo(System.Security.Cryptography.X509Certificates.X509NameType.DnsName, false);
				if (!string.IsNullOrEmpty(dns))
				{
					foreach (var allowed in AllowedHosts)
						if (HostMatches(allowed, dns)) return true;
				}
			}
			catch { /* fall through to Subject */ }

			try
			{
				string subject = cert.Subject ?? string.Empty;
				if (subject.IndexOf("byethost4.com", StringComparison.OrdinalIgnoreCase) >= 0) return true;
			}
			catch { /* ignore */ }

			return false;
		}

		private static bool HostMatches(string pattern, string host)
		{
			if (pattern == host) return true;
			if (pattern.StartsWith("*."))
			{
				string suffix = pattern.Substring(1); // ".byethost4.com"
				return host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) &&
					   host.Length > suffix.Length;
			}
			return false;
		}
	}

	/// <summary>
	/// Проверка/активация промокода на сервере (Giveaway).
	/// Сервер хранит коды в promos.json (не в клиенте) и одноразовость по client-id.
	/// </summary>
	public void Redeem(string code, Action<int, float, string> done)
	{
		StartCoroutine(SimplePost("?action=redeem&i=1", new List<IMultipartFormSection>
		{
			new MultipartFormDataSection("code", code ?? ""),
			new MultipartFormDataSection("client", ClientId())
		}, (body, err) =>
		{
			if (err != null) { done(0, 0, err); return; }
			WorkshopRedeemResponse p = null;
			try { p = JsonUtility.FromJson<WorkshopRedeemResponse>(StripJunk(body)); }
			catch (Exception e) { done(0, 0, e.Message); return; }
			if (p == null || !p.ok) { done(0, 0, p != null ? p.error : "redeem fail"); return; }
			done(p.cash, p.btc, null);
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

	// ================= Серверные аккаунты (account.php) =================

	public void AccountRegister(string name, string email, string password, Action<AccountAuthResponse, string> done)
	{
		StartCoroutine(RequestPostBase(AccountUrls, "?action=register&i=1", Form(
			("name", name), ("email", email), ("password", password), ("client", ClientId())),
			(body, err) => ParseAuth(body, err, done)));
	}

	public void AccountVerify(string email, string code, Action<AccountAuthResponse, string> done)
	{
		StartCoroutine(RequestPostBase(AccountUrls, "?action=verify&i=1", Form(
			("email", email), ("code", code), ("client", ClientId())),
			(body, err) => ParseAuth(body, err, done)));
	}

	public void AccountLogin(string login, string password, Action<AccountAuthResponse, string> done)
	{
		StartCoroutine(RequestPostBase(AccountUrls, "?action=login&i=1", Form(
			("login", login), ("password", password), ("client", ClientId())),
			(body, err) => ParseAuth(body, err, done)));
	}

	public void AccountResend(string email, Action<AccountAuthResponse, string> done)
	{
		StartCoroutine(RequestPostBase(AccountUrls, "?action=resend&i=1", Form(
			("email", email)),
			(body, err) => ParseAuth(body, err, done)));
	}

	public void AccountMe(string token, Action<AccountMeResponse, string> done)
	{
		StartCoroutine(RequestPostBase(AccountUrls, "?action=me&i=1", Form(
			("token", token)), (body, err) =>
		{
			if (err != null) { done(null, err); return; }
			AccountMeResponse p = null;
			try { p = JsonUtility.FromJson<AccountMeResponse>(StripJunk(body)); }
			catch (Exception e) { done(null, e.Message); return; }
			done(p, null);
		}));
	}

	public void AccountLogout(string token, Action<string> done)
	{
		StartCoroutine(RequestPostBase(AccountUrls, "?action=logout&i=1", Form(("token", token)),
			(body, err) => { done(err); }));
	}

	public void TgLink(string token, string telegram, Action<AccountAuthResponse, string> done)
	{
		StartCoroutine(RequestPostBase(AccountUrls, "?action=tg_link&i=1", Form(
			("token", token), ("telegram", telegram)),
			(body, err) => ParseAuth(body, err, done)));
	}

	private static void ParseAuth(string body, string err, Action<AccountAuthResponse, string> done)
	{
		if (err != null) { done(null, err); return; }
		AccountAuthResponse p = null;
		try { p = JsonUtility.FromJson<AccountAuthResponse>(StripJunk(body)); }
		catch (Exception e) { done(null, e.Message); return; }
		done(p, null);
	}

	private static List<IMultipartFormSection> Form(params (string, string)[] fields)
	{
		var form = new List<IMultipartFormSection>();
		foreach (var f in fields)
			form.Add(new MultipartFormDataSection(f.Item1, f.Item2 ?? ""));
		return form;
	}

	private IEnumerator RequestPostBase(IEnumerable<string> urls, string query, List<IMultipartFormSection> form, Action<string, string> done)
	{
		yield return BypassByethost();
		foreach (var baseUrl in urls)
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
					done(req.downloadHandler.text, null);
					yield break;
				}
				Debug.LogWarning("[Account] POST " + baseUrl + " -> " + req.error);
			}
		}
		done(null, "Empty reply from server");
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
		if (ServerAccounts.LoggedIn && !string.IsNullOrEmpty(ServerAccounts.Token))
			req.SetRequestHeader("X-Auth-Token", ServerAccounts.Token);
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
