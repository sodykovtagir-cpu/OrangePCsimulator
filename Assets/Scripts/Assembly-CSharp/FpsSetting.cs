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

			// Значение по умолчанию -- частота экрана, как и везде в игре.
			// Раньше здесь была своя логика с числом 30, и она перебивала
			// выбор, сделанный в основных настройках.
			// В PlayerPrefs не пишем: выбор игрока важнее авто-подстановки.
			int defaultFps = GraphicsBootstrap.TargetFpsDefault;

			if (settings == null || settings.Length == 0) return;

			int saved = PlayerPrefs.GetInt("TargetFps", defaultFps);

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
		Application.targetFrameRate = GraphicsBootstrap.TargetFps;
	}
}
