using UnityEngine;
using UnityEngine.UI;

public static class TextUtility
{
    public static void SetTextWithEllipsis(this Text textComponent, string value)
    {
        if (textComponent == null || string.IsNullOrEmpty(value))
            return;

        TextGenerator textGen = new TextGenerator();
        TextGenerationSettings settings = textComponent.GetGenerationSettings(textComponent.rectTransform.rect.size);

        string ellipsis = "...";
        string displayedText = value;

        while (!textGen.GetPreferredWidth(displayedText + ellipsis, settings).Equals(0f) &&
               textGen.GetPreferredWidth(displayedText + ellipsis, settings) > textComponent.rectTransform.rect.width)
        {
            if (displayedText.Length <= 1)
            {
                displayedText = string.Empty;
                break;
            }
            displayedText = displayedText.Substring(0, displayedText.Length - 1);
        }

        textComponent.text = string.IsNullOrEmpty(displayedText) ? "" : displayedText + ellipsis;
    }

    public static string TrimEnd(this string source, string value)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(value))
            return source;

        while (source.EndsWith(value))
        {
            source = source.Substring(0, source.Length - value.Length);
        }

        return source;
    }
}
