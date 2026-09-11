using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

// Owns account identity text from the first frame, before any network refresh.
// It reads the existing persisted session; cached display data never grants access.
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Text))]
public sealed class AccountProfileLabel : MonoBehaviour
{
    [SerializeField] private bool showEmail;
    private Text label;
    private Color normalColor;
    private bool colorCaptured;
    public static Color AdminColor { get { return new Color(1f, 0.2f, 0.2f, 1f); } }

    private void Awake()
    {
        label = GetComponent<Text>();
        Refresh();
    }

    private void OnEnable()
    {
        ServerAccounts.StateChanged += Refresh;
        Localization.LanguageChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        ServerAccounts.StateChanged -= Refresh;
        Localization.LanguageChanged -= Refresh;
    }

    public void Refresh()
    {
        if (label == null) label = GetComponent<Text>();
        if (label == null) return;
        if (!colorCaptured) { normalColor = label.color; colorCaptured = true; }
        label.text = showEmail ? MaskEmail(ServerAccounts.Email) : DisplayName();
        label.color = !showEmail && ServerAccounts.IsAdmin ? AdminColor : normalColor;
    }

    public static string DisplayName()
    {
        return ServerAccounts.LoggedIn ? ServerAccounts.Name : Localization.GetText("Not loggined");
    }

    // Hide at least half of the local part, keeping the domain recognizable.
    // Do not modify the real email used by sign-in / verification requests.
    public static string MaskEmail(string email)
    {
        if (string.IsNullOrEmpty(email)) return string.Empty;
        email = email.Trim();
        if (email.Length == 0) return string.Empty;
        int at = email.LastIndexOf('@');
        string local = at > 0 ? email.Substring(0, at) : email;
        string domain = at > 0 ? email.Substring(at) : string.Empty;
        var characters = StringInfo.ParseCombiningCharacters(local);
        int visible = characters.Length / 2;
        int prefixLength = visible > 0 ? characters[visible] : 0;
        return local.Substring(0, prefixLength) + new string('*', characters.Length - visible) + domain;
    }
}
