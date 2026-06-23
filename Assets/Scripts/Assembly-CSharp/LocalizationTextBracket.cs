using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Text))]
public class LocalizationTextBracket : LocalizationText
{
    public override void UpdateText()
    {
        string textToSet = null;

        if (id != null)
        {
            int openBracket = id.IndexOf('[');
            int closeBracket = id.LastIndexOf(']');

            if (openBracket == -1 || closeBracket == -1)
            {
                textToSet = Localization.GetText(id);
            }
            else
            {
                string key = id.Substring(openBracket + 1, closeBracket - openBracket - 1);
                string newValue = Localization.GetText(key);
                textToSet = id.Replace(key, newValue);
            }
        }

        if (text != null && textToSet != null)
        {
            text.text = textToSet;
        }
    }
}
