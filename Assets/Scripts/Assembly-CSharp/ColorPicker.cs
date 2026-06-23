using System;
using UnityEngine;
using UnityEngine.UI;

public class ColorPicker : MonoBehaviour
{
	[SerializeField]
	private Image preview;

	[SerializeField]
	private Slider redSlider;

	[SerializeField]
	private Slider greenSlider;

	[SerializeField]
	private Slider blueSlider;

	[SerializeField]
	private Text redText;

	[SerializeField]
	private Text greenText;

	[SerializeField]
	private Text blueText;

	[SerializeField]
	private InputField hexInput;

	private Action<Color> callback;

	private Color color;

	public void Cancel()
	{
		gameObject.SetActive(false);
	}

	public void OnEndEditHex(string text)
	{
		Color parsed = Color.clear;
		if (ColorUtility.TryParseHtmlString(text, out parsed))
		{
			SetColor(parsed);
		}
		else
		{
			if (hexInput != null) hexInput.text = "#" + ColorUtility.ToHtmlStringRGB(color);
		}
	}

	public void OnValueChangedRed(float value)
	{
		color.r = value;
		RefreshPreview();
	}

	public void OnValueChangedGreen(float value)
	{
		color.g = value;
		RefreshPreview();
	}

	public void OnValueChangedBlue(float value)
	{
		color.b = value;
		RefreshPreview();
	}

	public void PickColor(Color currentColor, Action<Color> callback)
	{
		gameObject.SetActive(true);
		this.callback = callback;
		SetColor(currentColor);
	}

	private void RefreshPreview()
	{
		if (preview != null) preview.color = color;
		if (redSlider != null) redSlider.value = color.r;
		if (greenSlider != null) greenSlider.value = color.g;
		if (blueSlider != null) blueSlider.value = color.b;

		int r = Mathf.RoundToInt(color.r * 255f);
		int g = Mathf.RoundToInt(color.g * 255f);
		int b = Mathf.RoundToInt(color.b * 255f);

		if (redText != null) redText.text = $"R:{r}";
		if (greenText != null) greenText.text = $"G:{g}";
		if (blueText != null) blueText.text = $"B:{b}";
		if (hexInput != null) hexInput.text = "#" + ColorUtility.ToHtmlStringRGB(color);
	}

	private void SetColor(Color newColor)
	{
		color = newColor;
		if (redSlider != null) redSlider.value = color.r;
		if (greenSlider != null) greenSlider.value = color.g;
		if (blueSlider != null) blueSlider.value = color.b;
		RefreshPreview();
	}

	public void Set()
	{
		callback?.Invoke(color);
		gameObject.SetActive(false);
	}
}
