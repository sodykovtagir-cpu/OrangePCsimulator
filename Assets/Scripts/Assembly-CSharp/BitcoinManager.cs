using System;
using System.CodeDom;
using UnityEngine;
using Yiming.AntiCheat;

public static class BitcoinManager
{
	public static float exchangeRate;

	public static FloatShadow Bitcoin { get; set; }

	static BitcoinManager()
	{
		exchangeRate = UnityEngine.Random.Range(3500, 4000);
		Bitcoin = PlayerPrefs.GetFloat("Bitcoin", 0);
		CheatingDetector.CheatDetected += OnCheaterDetected;
	}

	private static void OnCheaterDetected()
	{
		Debug.Log("Hacker!");
		Bitcoin = -100;
		PlayerPrefs.SetFloat("Bitcoin", Bitcoin);
		Application.Quit();
	}

	public static void Save()
	{
		PlayerPrefs.SetFloat("Bitcoin", Bitcoin);
	}
}
