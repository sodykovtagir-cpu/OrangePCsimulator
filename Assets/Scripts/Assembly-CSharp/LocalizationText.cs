using UnityEngine;
using UnityEngine.SocialPlatforms;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Text))]
public class LocalizationText : MonoBehaviour
{
	protected Text text;

	protected string id;

	private void Awake()
	{
		Localization.LanguageChanged += UpdateText;
		text = GetComponent<Text>();
		id = text.text;
		UpdateText();
	}

	private void OnDestroy()
	{
		Localization.LanguageChanged -= UpdateText;
	}

	public virtual void UpdateText()
	{
		if (text == null) text = GetComponent<Text>();
		if (text == null) return;
		text.text = Localization.GetText(id);
	}

	/// <summary>Меняет исходную строку (ключ / [ключ]) после программной смены текста.</summary>
	public void Bind(string source)
	{
		id = source;
		UpdateText();
	}
}
