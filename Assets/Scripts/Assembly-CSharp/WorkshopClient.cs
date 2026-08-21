using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
		using (var req = UnityWebRequest.Get(ApiUrl + "?action=list"))
		{
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
		using (var req = UnityWebRequest.Get(ApiUrl + "?action=download&id=" + item.id))
		{
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
}
