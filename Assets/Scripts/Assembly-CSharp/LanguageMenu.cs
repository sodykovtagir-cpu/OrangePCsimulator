using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class LanguageMenu : MonoBehaviour
{
    [SerializeField] private GameObject languagePrefab;
    [SerializeField] private Transform languageParent;
    [SerializeField] private Text translatorsText;
    [SerializeField] private ToggleGroup toggleGroup;

    private TextAnimation[] textAnimations;

    private void Start()
    {
        Localization.CreateContent();


        textAnimations = FindObjectsOfType<TextAnimation>(includeInactive: true);

        string[] allLanguages    = Localization.GetRow("*Short Form");
        string[] translatedNames = Localization.GetRow("*Language Translated");
        string[] nativeNames     = Localization.GetRow("*Language");
        string[] statusRows      = Localization.GetRow("*Status");
        string[] translatorsRows = Localization.GetRow("*Translator");


        var sb = new StringBuilder();

		for (int i = 0; i < allLanguages.Length; i++)
		{
			string iso = allLanguages[i];
			string display = i < translatedNames.Length ? translatedNames[i] : iso;
			string native = i < nativeNames.Length ? nativeNames[i] : "";
			string status = i < statusRows.Length ? statusRows[i] : "";
			string translatorsCsv = i < translatorsRows.Length ? translatorsRows[i] : "";

			GameObject go = Instantiate(languagePrefab, languageParent);
			if (!go)
			{
				continue;
			}

			Toggle toggle = go.GetComponent<Toggle>();
			if (!toggle)
			{
				Destroy(go);
				continue;
			}
			toggle.group = toggleGroup;

			Text label = go.GetComponentInChildren<Text>();
			if (label) label.text = display;

			ToggleEffect effect = go.GetComponent<ToggleEffect>();
			if (effect)
			{
				effect.selected.AddListener(() => ChangeLanguage(iso));
				bool isCurrent = string.Equals(iso, Localization.GetLanguage(),
											  System.StringComparison.Ordinal);
				effect.SetIsOn(isCurrent, false);
			}

			if (i != 0) sb.AppendLine();
			sb.Append("<color=orange>");
			sb.Append(display);
			sb.Append(" [");
			sb.Append(status);
			sb.Append("%]</color> \n");
			if (!string.IsNullOrEmpty(translatorsCsv))
			{
				string[] translators = translatorsCsv.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
				if (translators.Length > 0)
				{
					sb.Append(string.Join("\n", translators));
				}
			}
			sb.Append('\n');
        }

        if (translatorsText) translatorsText.text = sb.ToString();
    }

    private void ChangeLanguage(string iso)
    {
        PlayerPrefs.SetString("Language", iso);
        Localization.SetLanguage(iso);
        if (textAnimations != null)
        {
            foreach (var anim in textAnimations) anim?.ResetText();
        }
    }

    public LanguageMenu() { }
}
