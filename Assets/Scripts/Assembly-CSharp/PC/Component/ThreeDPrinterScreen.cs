using UnityEngine;
using UnityEngine.UI;

namespace PC.Component
{
	/// <summary>
	/// Small UI on the printer body: what is printing, time left, filament left.
	/// All references are optional so the logic works before the model is ready.
	/// </summary>
	public class ThreeDPrinterScreen : MonoBehaviour
	{
		[SerializeField] private Text stateText;
		[SerializeField] private Text modelNameText;
		[SerializeField] private Text timeText;
		[SerializeField] private Text filamentText;
		[SerializeField] private Image progressFill;

		public void ShowIdle(float filamentMeters)
		{
			Set("Idle", "No model", "--:--", string.Empty, 0f);
		}

		public void ShowPrinting(string modelName, float remainingSeconds, float filamentMeters, float progress)
		{
			Set("Printing", modelName, FormatTime(remainingSeconds), string.Empty, progress);
		}

		public void ShowWaitingForFilament(string modelName, float remainingSeconds, float progress)
		{
			Set(Localization.GetText("Replace filament spool"), modelName, FormatTime(remainingSeconds), string.Empty, progress);
		}

		public void ShowFinished(string modelName, float filamentMeters)
		{
			Set("Finished", modelName, "00:00", string.Empty, 1f);
		}

		public void ShowError(string message, float filamentMeters)
		{
			Set("Error", message, "--:--", string.Empty, 0f);
		}

		private void Set(string state, string model, string time, string filament, float progress)
		{
			if (stateText != null) stateText.text = state;
			if (modelNameText != null) modelNameText.text = model;
			if (timeText != null) timeText.text = time;
			if (filamentText != null)
			{
				filamentText.text = string.Empty;
				filamentText.gameObject.SetActive(false);
			}
			if (progressFill != null)
			{
				// The user-authored screen may still have a Sliced image; force a
				// horizontal filled bar so fillAmount actually shows progress.
				progressFill.type = Image.Type.Filled;
				progressFill.fillMethod = Image.FillMethod.Horizontal;
				progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
				progressFill.fillAmount = Mathf.Clamp01(progress);
			}
		}

		public static string FormatTime(float seconds)
		{
			int total = Mathf.CeilToInt(Mathf.Max(0f, seconds));
			int minutes = total / 60;
			int secs = total % 60;
			return minutes.ToString("00") + ":" + secs.ToString("00");
		}

		public static string FormatFilament(float meters)
		{
			return Mathf.Max(0f, meters).ToString("0.00") + " m";
		}
	}
}
