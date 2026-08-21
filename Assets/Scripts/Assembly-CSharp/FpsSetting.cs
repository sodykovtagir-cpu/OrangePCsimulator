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
		try
		{
			int maxRefreshRate = 60;
			try { maxRefreshRate = Screen.currentResolution.refreshRate; } catch { }

			if (maxRefreshRate > 0 && 60 > maxRefreshRate + 1)
			{
				PlayerPrefs.SetInt("TargetFps", 30);
				Application.targetFrameRate = 30;
			}

			if (settings == null || settings.Length == 0) return;

			int saved = PlayerPrefs.GetInt("TargetFps", 60);

			for (int i = 0; i < settings.Length; i++)
			{
				var x = settings[i];
				if (x.button == null) continue;

				var toggle = x.button.GetComponent<Toggle>();
				if (toggle != null)
				{
					int fps = x.fps;
					toggle.onValueChanged.AddListener(v => { if (v) SetFps(fps); });
				}

				if (x.fps == saved)
				{
					var effect = x.button.GetComponent<ToggleEffect>();
					if (effect != null)
						effect.SetIsOn(true, false);
					else if (toggle != null)
						toggle.SetIsOnWithoutNotify(true);
				}
			}

			for (int i = 0; i < settings.Length; i++)
			{
				var f = settings[i];
				if (f.button == null) continue;
				if (maxRefreshRate > 0 && f.fps > maxRefreshRate + 1)
					f.button.SetActive(false);
			}
		}
		catch (Exception e)
		{
			Debug.LogWarning("[FpsSetting] " + e.Message);
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
