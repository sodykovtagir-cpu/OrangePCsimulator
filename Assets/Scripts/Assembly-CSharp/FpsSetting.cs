using System;
using UnityEngine;
using UnityEngine.UI;
public class FpsSetting : MonoBehaviour
{
	[Serializable]
	private struct Fps
	{
		public GameObject button;

		public int fps;
	}

	[SerializeField]
	private Fps[] settings;

	private void Awake()
	{
		int maxRefreshRate = Screen.currentResolution.refreshRate;
		if (60 > maxRefreshRate + 1)
		{
			PlayerPrefs.SetInt("TargetFps", 30);
			Application.targetFrameRate = 30;
		}

		foreach (var x in settings)
		{
			if (x.button.GetComponent<Toggle>() != null)
			{
				x.button.GetComponent<Toggle>().onValueChanged.AddListener((bool v) => { if (v) SetFps(x.fps); });
			}
			if (x.fps == PlayerPrefs.GetInt("TargetFps", 60))
			{
				x.button.GetComponent<ToggleEffect>().SetIsOn(true, false);
			}
		}

		foreach (var f in settings)
		{
			if (f.fps > maxRefreshRate + 1)
			{
				f.button.SetActive(false);
			}
		}
	}

	public void SetFps(int targetFps)
	{
		Application.targetFrameRate = targetFps;
        PlayerPrefs.SetInt("TargetFps", targetFps);
        PlayerPrefs.Save();
	}

	public static void RestoreSetting()
	{
		int savedFps = PlayerPrefs.GetInt("TargetFps", 60);
        Application.targetFrameRate = savedFps;
	}
}
