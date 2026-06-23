using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using SaveManagement;
using System.IO;
public class Gallery : MonoBehaviour
{
	private class SaveInfo
	{
		public string id;

		public string icon;

		public string author;

		public string title;

		public string description;

		public string version;

		public int uploadTime;

		public int downloads;
	}

	[SerializeField]
	private string url;

	[SerializeField]
	private GalleryItem galleryItem;

	[SerializeField]
	private Transform galleryParent;

	[SerializeField]
	private GameObject loading;

	[SerializeField]
	private MainMenu mainMenu;

	private void Start()
	{
		StartCoroutine("ListFiles");
	}

	private IEnumerator ListFiles()
	{
		loading.SetActive(true);
		UnityWebRequest req = UnityWebRequest.Get(url + "/list");
		yield return req.SendWebRequest();
		if (req.result != UnityWebRequest.Result.Success)
		{
			loading.SetActive(false);
			yield break;
		}
		string json = req.downloadHandler.text;
		Debug.Log(json);
		List<SaveInfo> ls = new List<SaveInfo>();
		var pd = JArray.Parse(json);
		foreach (JToken j in pd)
		{
			var it = new SaveInfo();
			it.author = j.Value<string>("author");
			it.icon = j.Value<string>("icon");
			it.description = j.Value<string>("description");
			it.uploadTime = j.Value<int>("uploadTime");
			it.downloads = j.Value<int>("downloads");
			it.id = j.Value<string>("id");
			it.version = j.Value<string>("version");
			it.title = j.Value<string>("title");
			ls.Add(it);
		}

		foreach (SaveInfo c in ls)
		{
			var obj = Instantiate(galleryItem, galleryParent);
			obj.title.text = c.title;
			obj.description.text = c.description;
			obj.uploadTime.text = DateTimeOffset.FromUnixTimeSeconds(c.uploadTime).DateTime.ToString("MM/dd/yyyy");
			obj.author.text = c.author;
			obj.info.text = string.Format("{0} Downloads, Version {1}", c.downloads, c.version);
			StartCoroutine(GetTexture(c.icon, obj.icon));
			obj.download.onClick.AddListener(() => StartCoroutine(Download(c)));
		}
		loading.SetActive(false);
	}

	private IEnumerator Download(SaveInfo info)
	{
		loading.SetActive(true);
		UnityWebRequest req = UnityWebRequest.Get(url + $"/download/{info.id}");
		yield return req.SendWebRequest();
		string firebaseUrl = req.downloadHandler.text.Trim();
		string path = SaveUtility.GetNewPath(info.title);
		yield return DownloadFile(path, firebaseUrl);
		loading.SetActive(false);
		DataLoader l = new DataLoader(path);
		l.LoadFromPath();
		mainMenu.LoadFile(l);
	}

	private IEnumerator DownloadFile(string fileName, string url)
	{
		string folderPath = SaveUtility.GetFolderPath();
		string fullPath = Path.Combine(folderPath, fileName);
		using (UnityWebRequest uwr = UnityWebRequest.Get(url))
		{
			yield return uwr.SendWebRequest();
			File.WriteAllBytes(fullPath, uwr.downloadHandler.data);
		}
	}

	private IEnumerator GetTexture(string url, RawImage image)
	{
		using (UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(url))
		{
			yield return uwr.SendWebRequest();
			Texture2D texture = DownloadHandlerTexture.GetContent(uwr);
			image.texture = texture;
			image.SetNativeSize();
		}
	}
}
