using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using SaveManagement;
using System.IO;
using UnityEngine.Networking;

[RequireComponent(typeof(AudioSource))]
public class MainMenu : MonoBehaviour
{
	[SerializeField]
	private Text title;

	[SerializeField]
	private Text loadingText;

	[SerializeField]
	private int startRoomSceneIndex;

	[SerializeField]
	private MenuManager menuManager;

	[SerializeField]
	private GameObject guideToTutorial;

	[SerializeField]
	private int tutorialVersion;

	private bool showLine;

	public static MainMenu Instance { get; private set; }

	private void Awake()
	{
		Instance = this;
		Localization.CreateContent();
		Localization.SetLanguage(PlayerPrefs.GetString("Language", GetSystemLanguage()));
		Localization.LanguageChanged += () =>
		{
			PlayerPrefs.SetString("Language", Localization.GetLanguage());	
		};
	}

	private void Start()
	{
		FpsSetting.RestoreSetting();
		ResolutionSetting.RestoreSetting();
		if (PlayerPrefs.GetInt("TutorialVersion", -1) < tutorialVersion)
		{
			guideToTutorial.SetActive(true);
		}
		else
		{
			guideToTutorial.SetActive(false);
		}

		AudioListener.volume = PlayerPrefs.GetFloat("Volume", 1f);
		InvokeRepeating(nameof(Blinking), 0.5f, 0.5f);
	}

	private static string GetSystemLanguage()
	{
		switch (Application.systemLanguage)
		{
			case SystemLanguage.Afrikaans: return "AF";
			case SystemLanguage.Arabic: return "AR";
			case SystemLanguage.Basque: return "EU";
			case SystemLanguage.Belarusian: return "BE";
			case SystemLanguage.Bulgarian: return "BG";
			case SystemLanguage.Catalan: return "CA";
			case SystemLanguage.Chinese:
			case SystemLanguage.ChineseSimplified:
			case SystemLanguage.ChineseTraditional: return "ZH-CN";
			case SystemLanguage.Czech: return "CS";
			case SystemLanguage.Danish: return "DA";
			case SystemLanguage.Dutch: return "NL";
			case SystemLanguage.English: return "EN";
			case SystemLanguage.Estonian: return "ET";
			case SystemLanguage.Faroese: return "FO";
			case SystemLanguage.Finnish: return "FI";
			case SystemLanguage.French: return "FR";
			case SystemLanguage.German: return "DE";
			case SystemLanguage.Greek: return "EL";
			case SystemLanguage.Hebrew: return "HE";
			case SystemLanguage.Hungarian: return "HU";
			case SystemLanguage.Icelandic: return "IS";
			case SystemLanguage.Indonesian: return "ID";
			case SystemLanguage.Italian: return "IT";
			case SystemLanguage.Japanese: return "JA";
			case SystemLanguage.Korean: return "KO";
			case SystemLanguage.Latvian: return "LV";
			case SystemLanguage.Lithuanian: return "LT";
			case SystemLanguage.Norwegian: return "NO";
			case SystemLanguage.Polish: return "PL";
			case SystemLanguage.Portuguese: return "PT";
			case SystemLanguage.Romanian: return "RO";
			case SystemLanguage.Russian: return "RU";
			case SystemLanguage.SerboCroatian: return "SH";
			case SystemLanguage.Slovak: return "SK";
			case SystemLanguage.Slovenian: return "SL";
			case SystemLanguage.Spanish: return "ES";
			case SystemLanguage.Swedish: return "SV";
			case SystemLanguage.Thai: return "TH";
			case SystemLanguage.Turkish: return "TR";
			case SystemLanguage.Ukrainian: return "UK";
			case SystemLanguage.Vietnamese: return "VI";
			default: return "EN";
		}
	}

private void Blinking()
{
    showLine = !showLine;

    title.text = showLine
        ? "<color=#FF8800>Orange PC</color> Simulator_"
        : "<color=#FF8800>Orange PC</color> Simulator";
}
	public void LoadExample(string name)
	{
		string path = Path.Combine(Application.streamingAssetsPath, "Examples", name + ".pc");
	#if UNITY_ANDROID || UNITY_WEBGL
		string fileContents = null;
		var req = UnityEngine.Networking.UnityWebRequest.Get(path);
		var op = req.SendWebRequest();
		while (!op.isDone) { }
		if (req.result == UnityWebRequest.Result.Success)
		{
			fileContents = req.downloadHandler.text;
		}
	#else
		string fileContents = File.ReadAllText(path);
	#endif
		DataLoader lod = new DataLoader();
		lod.LoadFromString(fileContents);
		LoadFile(lod);
	}

	public void LoadFile(DataLoader loader)
	{
		SaveManager.Loader = loader;
		int roomType = loader.GameData.room;
		LoadScene(roomType);
	}

	public void Tutorial()
	{
		PlayerPrefs.SetInt("TutorialVersion", tutorialVersion);
		LoadScene("Tutorial");
	}

	public void LoadScene(int sceneBuildIndex)
	{
		StartCoroutine(LoadAsync(SceneManager.LoadSceneAsync(startRoomSceneIndex + sceneBuildIndex)));
	}

	public void LoadScene(string sceneName)
	{
		StartCoroutine(LoadAsync(SceneManager.LoadSceneAsync(sceneName)));
	}

	private IEnumerator LoadAsync(AsyncOperation operation)
	{
		menuManager.ShowMenu("Loading");
		operation.allowSceneActivation = false;

		while (!operation.isDone)
		{
			float progress = Mathf.Clamp01(operation.progress / 0.9f) * 100f;
			loadingText.text = Localization.GetText("Loading...") + "\n[" + Mathf.RoundToInt(progress) + "%]";

			if (operation.progress >= 0.9f)
			{
				loadingText.text = Localization.GetText("Loading...") + "\n[100%]";
				yield return new WaitForSeconds(0.5f);
				operation.allowSceneActivation = true;
			}

			yield return null;
		}
	}

	public void Exit()
	{
		Application.Quit();
	}
}
