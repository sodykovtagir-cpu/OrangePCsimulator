using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(LayoutElement))]
[ExecuteInEditMode]
[RequireComponent(typeof(InputField))]
public class InputFieldMod : UIBehaviour
{
	[Range(1f, 50f)]
	public int textRows =1;

	public ScrollRect scrollRect;

	private RectTransform scrollRectTransform;

	private Canvas scaler;

	private HorizontalOrVerticalLayoutGroup parentLayout;

	private LayoutElement inputElement;

	private InputField inputField;

	private RectTransform rect;

	private CanvasRenderer caret;

	private RectTransform ScrollRectTransform => scrollRectTransform;

	private float ScaleFactor => scaler.scaleFactor;

	private HorizontalOrVerticalLayoutGroup ParentLayout => parentLayout;

	private LayoutElement InputElement => inputElement;

	private InputField InputField => inputField;

	private RectTransform Rect => rect;

	private float VerticalOffset
	{
		get
		{
			var input = InputField;
			if (input == null) return 0f;

			var text = input.textComponent;
			if (text == null) return 0f;

			var rect = text.rectTransform;
			if (rect == null) return 0f;

			var min = rect.offsetMin;
			var max = rect.offsetMax;

			return min.y - max.y;
		}
	}

	protected override void Start()
	{
		var input = InputField;
		if (input == null) return;

		var onChange = input.onValueChanged;
		if (onChange == null) return;

		onChange.AddListener(ResizeInput);
	}

	private void Update()
	{
		if (caret != null) return;

		var input = InputField;
		if (input == null) return;

		var root = input.transform;
		var selfTransform = input.transform;
		if (root == null || selfTransform == null) return;

		var caretName = selfTransform.name + " Input Caret";
		var caretTransform = root.Find(caretName);
		if (caretTransform == null) return;

		var graphic = caretTransform.GetComponent<Graphic>();
		if (graphic != null) return;

		var go = caretTransform.gameObject;
		if (go == null) return;

		var img = go.AddComponent<Image>();
		img.color = new Color(0f, 0f, 0f, 0f);
	}

	private void ResizeInput()
    {
        ResizeInput(inputField.text);
    }

	private void ResizeInput(string text)
	{
		var input = InputField;
		if (input == null) return;

		var textComponent = input.textComponent;
		if (textComponent == null) return;

		var rect = textComponent.rectTransform;
		if (rect == null) return;

		var rectSize = rect.rect.size;

		var content = string.IsNullOrEmpty(text) ? input.text : text;
		if (string.IsNullOrEmpty(content)) content = " ";

		var generator = new TextGenerator();
		var settings = textComponent.GetGenerationSettings(rectSize);
		var preferredHeight = generator.GetPreferredHeight(content, settings);

		var scale = ScaleFactor;
		var offset = VerticalOffset;

		var targetHeight = preferredHeight / scale + offset;
		float limitHeight;

		if (scrollRect != null)
		{
			var scrollRectTransform = ScrollRectTransform;
			if (scrollRectTransform == null) return;

			limitHeight = scrollRectTransform.rect.height;
			targetHeight = Mathf.Min(targetHeight, limitHeight);
		}
		else
		{
			var oneLineGenerator = new TextGenerator();
			var oneLineSettings = textComponent.GetGenerationSettings(rectSize);
			var oneLineHeight = oneLineGenerator.GetPreferredHeight(" ", oneLineSettings);

			limitHeight = offset + (oneLineHeight * textRows) / scale;
			targetHeight = Mathf.Max(targetHeight, limitHeight);
		}

		var parentLayout = ParentLayout;
		if (parentLayout != null)
		{
			var element = InputElement;
			if (element == null) return;
			element.preferredHeight = targetHeight;
		}
		else
		{
			var selfRect = Rect;
			if (selfRect == null) return;

			var width = selfRect.rect.width;
			selfRect.sizeDelta = new Vector2(width, targetHeight);
		}

		if (scrollRect != null)
		{
			var routine = ScrollMax();
			if (routine != null) StartCoroutine(routine);
		}
	}

	private IEnumerator ScrollMax()
	{
		yield return new WaitForEndOfFrame();
		if (scrollRect != null)
			scrollRect.verticalNormalizedPosition = 0f;
	}
}
