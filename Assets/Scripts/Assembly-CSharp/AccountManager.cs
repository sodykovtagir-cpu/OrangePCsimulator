using UnityEngine;

/// <summary>
/// Локальная система аккаунтов для мастерской.
/// Аккаунты хранятся в PlayerPrefs (пароль — FNV-1a хеш, не в открытом виде).
/// Без сервера это защита «от честных»: аккаунты не синхронизируются между устройствами,
/// но дают механизм «вошёл/не вошёл» и привязку имени автора.
/// </summary>
public static class AccountManager
{
	private const string CurrentKey = "Acct_Current";
	private const string PwPrefix = "Acct_Pw_";

	public static string CurrentUser
	{
		get { return PlayerPrefs.GetString(CurrentKey, ""); }
	}

	public static bool IsLoggedIn()
	{
		return !string.IsNullOrEmpty(CurrentUser);
	}

	/// <summary>Вход в существующий аккаунт.</summary>
	public static bool Login(string name, string password)
	{
		if (string.IsNullOrEmpty(name) || password == null) return false;
		string key = PwPrefix + name.Trim().ToLowerInvariant();
		string stored = PlayerPrefs.GetString(key, "");
		if (string.IsNullOrEmpty(stored)) return false;              // аккаунта нет
		if (!string.Equals(stored, Hash(password), System.StringComparison.Ordinal)) return false;
		PlayerPrefs.SetString(CurrentKey, name.Trim());
		PlayerPrefs.Save();
		return true;
	}

	/// <summary>Регистрация нового аккаунта (имя должно быть уникальным).</summary>
	public static bool Register(string name, string password)
	{
		if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(password)) return false;
		string clean = name.Trim();
		if (clean.Length < 2) return false;
		if (clean.Length > 24) return false;
		string key = PwPrefix + clean.ToLowerInvariant();
		if (!string.IsNullOrEmpty(PlayerPrefs.GetString(key, ""))) return false; // занято
		PlayerPrefs.SetString(key, Hash(password));
		PlayerPrefs.SetString(CurrentKey, clean);
		PlayerPrefs.Save();
		return true;
	}

	public static void Logout()
	{
		PlayerPrefs.DeleteKey(CurrentKey);
		PlayerPrefs.Save();
	}

	/// <summary>FNV-1a хеш — просто чтобы не хранить пароль открытым текстом.</summary>
	private static string Hash(string s)
	{
		unchecked
		{
			uint h = 2166136261u;
			for (int i = 0; i < s.Length; i++)
			{
				h ^= s[i];
				h *= 16777619u;
			}
			return h.ToString("X8");
		}
	}
}
