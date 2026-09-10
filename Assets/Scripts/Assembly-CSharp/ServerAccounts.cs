using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Persisted session/display identity, with server-owned saves kept in memory only.
/// AccountMe validates the token; cached display data is not an authorization grant.
/// </summary>
public static class ServerAccounts
{
    private const string TokenKey = "SrvAcct_Token";
    private const string NameKey = "SrvAcct_Name";
    private const string EmailKey = "SrvAcct_Email";
    private const string BonusKey = "SrvAcct_Bonus";
    private const string AdminKey = "SrvAcct_Admin";

    public static event System.Action StateChanged;
    private static readonly List<AccountSaveItem> saves = new List<AccountSaveItem>();

    public static string Token { get { return PlayerPrefs.GetString(TokenKey, ""); } }
    public static string Name { get { return PlayerPrefs.GetString(NameKey, ""); } }
    public static string Email { get { return PlayerPrefs.GetString(EmailKey, ""); } }
    public static bool LoggedIn { get { return !string.IsNullOrEmpty(Token) && !string.IsNullOrEmpty(Name); } }
    public static bool BonusClaimed { get { return PlayerPrefs.GetInt(BonusKey, 0) == 1; } }
    // Cached role for UI/game features. Privileged server actions must validate the token server-side.
    public static bool IsAdmin { get { return LoggedIn && PlayerPrefs.GetInt(AdminKey, 0) == 1; } }
    public static bool SavesLoaded { get; private set; }
    public static IReadOnlyList<AccountSaveItem> MySaves { get { return saves; } }

    public static bool IsCurrentSession(string token)
    {
        return !string.IsNullOrEmpty(token) && string.Equals(Token, token, System.StringComparison.Ordinal);
    }

    public static AccountSaveItem FindSave(int id)
    {
        if (id <= 0) return null;
        for (int i = 0; i < saves.Count; i++)
            if (saves[i] != null && saves[i].id == id) return saves[i];
        return null;
    }

    public static bool OwnsListing(int id) { return FindSave(id) != null; }
    public static string OwnerKeyFor(int id)
    {
        var save = FindSave(id);
        return save != null ? (save.owner_key ?? "") : "";
    }

    public static void SetSession(string token, string name, string email, bool isAdmin = false)
    {
        token = token ?? "";
        if (!string.Equals(Token, token, System.StringComparison.Ordinal))
        {
            // Never carry another account's saves, bonus or email into a new login.
            saves.Clear();
            SavesLoaded = false;
            PlayerPrefs.DeleteKey(NameKey);
            PlayerPrefs.DeleteKey(EmailKey);
            PlayerPrefs.DeleteKey(BonusKey);
        }
        if (string.IsNullOrEmpty(token)) PlayerPrefs.DeleteKey(TokenKey);
        else PlayerPrefs.SetString(TokenKey, token);
        if (!string.IsNullOrEmpty(name)) PlayerPrefs.SetString(NameKey, name);
        if (!string.IsNullOrEmpty(email)) PlayerPrefs.SetString(EmailKey, email);
        PlayerPrefs.SetInt(AdminKey, !string.IsNullOrEmpty(token) && isAdmin ? 1 : 0);
        PlayerPrefs.Save();
        StateChanged?.Invoke();
    }

    // Apply one response atomically and ignore responses from a previous login.
    public static bool TryApplyProfile(string requestedToken, AccountMeResponse profile)
    {
        if (!IsCurrentSession(requestedToken) || profile == null || !profile.ok) return false;
        if (!string.IsNullOrEmpty(profile.name)) PlayerPrefs.SetString(NameKey, profile.name);
        if (!string.IsNullOrEmpty(profile.email)) PlayerPrefs.SetString(EmailKey, profile.email);
        PlayerPrefs.SetInt(BonusKey, profile.tg_bonus ? 1 : 0);
        PlayerPrefs.SetInt(AdminKey, profile.is_admin ? 1 : 0);
        saves.Clear();
        if (profile.saves != null)
            foreach (var save in profile.saves)
                if (save != null) saves.Add(save);
        SavesLoaded = true;
        PlayerPrefs.Save();
        StateChanged?.Invoke();
        return true;
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
        if (list != null)
            foreach (var save in list)
                if (save != null) saves.Add(save);
        SavesLoaded = true;
        StateChanged?.Invoke();
    }

    public static void Clear()
    {
        PlayerPrefs.DeleteKey(TokenKey);
        PlayerPrefs.DeleteKey(NameKey);
        PlayerPrefs.DeleteKey(EmailKey);
        PlayerPrefs.DeleteKey(BonusKey);
        PlayerPrefs.DeleteKey(AdminKey);
        saves.Clear();
        SavesLoaded = false;
        PlayerPrefs.Save();
        StateChanged?.Invoke();
    }
}
