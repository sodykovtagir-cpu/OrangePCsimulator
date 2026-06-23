using System;
using UnityEngine;
using UnityEngine.UI;

public class ProgressBar : MonoBehaviour
{
	[SerializeField]
	private Text progressTitle;

	[SerializeField]
	private Slider progressSlider;

	[SerializeField]
	private Image progressImage;

	[SerializeField]
	private Button progressButton;

	private Action cancelCallback;

	public void CallProgressBar(string name, UnityEngine.Color barColor, System.Action cancelCallback)
	{
		if (progressTitle != null) progressTitle.text = name;
		if (progressImage != null) progressImage.color = barColor;
		this.cancelCallback = cancelCallback;
		var btn = progressButton;
		if (btn != null) btn.interactable = cancelCallback != null;
		var go = gameObject;
		if (go != null) go.SetActive(true);
	}

	public void SetProgress(float value)
	{
		if (progressSlider != null) progressSlider.value = value;
	}

	public void CancelProgress()
	{
		var cb = cancelCallback;
		if (cb == null) return;
		cb();
		var go = gameObject;
		if (go != null) go.SetActive(false);
	}

	public void CloseProgressBar()
	{
		var go = gameObject;
		if (go != null) go.SetActive(false);
	}
}
