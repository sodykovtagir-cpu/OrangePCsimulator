using UnityEngine;
using UnityEngine.SocialPlatforms;
using UnityEngine.UI;

public class AboutMenu : MonoBehaviour
{
	[SerializeField]
	private Text version;

	[SerializeField]
	private Text score;

	[SerializeField]
	private Text creatorText;

	[SerializeField]
	private Text bitcoinText;

	private void Awake()
	{
		Refresh();
		Localization.LanguageChanged += Refresh;
	}

	private void OnEnable()
	{
		Refresh();
	}

	private void OnDestroy()
	{
		Localization.LanguageChanged -= Refresh;
	}

	private void Refresh()
	{
		version.text = Localization.GetText("Version") + "\n" + Application.version;
		score.text = Localization.GetText("Highest Benchmark") + " : <color=lime>" + PlayerPrefs.GetInt("Score", 0).ToString() + "</color>";
		creatorText.text = string.Format(Localization.GetText("Made By {0}"), "<color=orange>@orangePCsimu</color>");
		bitcoinText.text = BitcoinManager.Bitcoin.ToString() + " BTC";
	}
}
