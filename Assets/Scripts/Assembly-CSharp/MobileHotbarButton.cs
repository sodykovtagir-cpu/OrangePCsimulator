using UnityEngine;

public class MobileHotbarButton : MonoBehaviour
{
    public string buttonId;
    public string displayName;
    public string localizationKey;

    public string GetDisplayName()
    {
        if (!string.IsNullOrEmpty(localizationKey))
            return Localization.GetText(localizationKey);

        if (!string.IsNullOrEmpty(displayName))
            return displayName;

        return buttonId;
    }
}