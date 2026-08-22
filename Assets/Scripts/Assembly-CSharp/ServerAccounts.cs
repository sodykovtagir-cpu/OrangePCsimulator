using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Серверные аккаунты (двухэтапка: регистрация с email -> код -> вход).
/// Сессия хранится токеном в PlayerPrefs -> автовход между запусками.
/// Состояние доступно всем; сеть идёт через WorkshopClient.
/// </summary>
public static class ServerAccounts
{
	private const string TokenKey = "SrvAcct_Token";
	private const string NameKey  = "SrvAcct_Name";
	private const string EmailKey = "SrvAcct_Email";
	private const string BonusKey = "SrvAcct_Bonus";

	/// <summary>Вызывается при входе/выходе/обновлении профиля.</summary>
	public static event System.Action StateChanged;

	private static readonly List<AccountSaveItem> saves = new List<AccountSaveItem>();

	public static string Token { get { return PlayerPrefs.GetString(TokenKey, ""); } }
	public static string Name   { get { return PlayerPrefs.GetString(NameKey, ""); } }
	public static string Email  { get { return PlayerPrefs.GetString(EmailKey, ""); } }
	public static bool LoggedIn { get { return !string.IsNullOrEmpty(Token) && !string.IsNullOrEmpty(Name); } }
	public static bool BonusClaimed { get { return PlayerPrefs.GetInt(BonusKey, 0) == 1; } }

	public static IReadOnlyList<AccountSaveItem> MySaves { get { return saves; } }

	public static void SetSession(string token, string name, string email)
	{
		if (!string.IsNullOrEmpty(token)) PlayerPrefs.SetString(TokenKey, token);
		if (!string.IsNullOrEmpty(name))  PlayerPrefs.SetString(NameKey, name);
		if (!string.IsNullOrEmpty(email)) PlayerPrefs.SetString(EmailKey, email);
		PlayerPrefs.Save();
		StateChanged?.Invoke();
	}

	public static void SetBonusClaimed()
	{
		PlayerPrefs.SetInt(BonusKey, 1);
		PlayerPrefs.Save();
		StateChanged?.Invoke();
	}

	public static void SetSaves(List<AccountSaveItem> list)
	{
		saves.Clear();
		if (list != null) saves.AddRange(list);
		StateChanged?.Invoke();
	}

	public static void Clear()
	{
		PlayerPrefs.DeleteKey(TokenKey);
		PlayerPrefs.DeleteKey(NameKey);
		PlayerPrefs.DeleteKey(EmailKey);
		PlayerPrefs.DeleteKey(BonusKey);
		saves.Clear();
		PlayerPrefs.Save();
		StateChanged?.Invoke();
	}
}
