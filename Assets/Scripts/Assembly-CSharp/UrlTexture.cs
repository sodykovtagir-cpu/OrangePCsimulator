using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Networking;

public class UrlTexture : MonoBehaviour
{
	[SerializeField]
	private string imageSource;

	[SerializeField]
	private Renderer[] screens;

	private void Start()
	{
		StartCoroutine(GetText());
	}

	private IEnumerator GetText()
	{
		var req = UnityWebRequest.Get(imageSource);
		yield return req.SendWebRequest();
		if (req.result != UnityWebRequest.Result.Success)
		{
			UnityEngine.Debug.Log(req.error);
			yield break;
		}
		var text = req.downloadHandler.text;
		if (string.IsNullOrEmpty(text) || screens == null || screens.Length == 0) yield break;
		var parts = text.Split(',');
		var count = Math.Min(screens.Length, parts.Length);
		for (int i = 0; i < count; i++)
		{
			var url = parts[i].Trim();
			if (string.IsNullOrEmpty(url) || screens[i] == null) continue;
			yield return StartCoroutine(GetTexture(url, screens[i]));
		}
	}

	private IEnumerator GetTexture(string url, Renderer screen)
	{
		using (var uwr = UnityWebRequestTexture.GetTexture(url))
		{
			yield return uwr.SendWebRequest();
			if (uwr.result != UnityWebRequest.Result.Success) yield break;
			var tex = DownloadHandlerTexture.GetContent(uwr);
			if (screen != null && screen.material != null) screen.material.mainTexture = tex;
		}
	}
}
