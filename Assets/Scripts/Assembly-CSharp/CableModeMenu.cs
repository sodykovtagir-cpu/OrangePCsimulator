using PC.Component.Cabling;
using UnityEngine;
using UnityEngine.UI;

// Attach this component to the editable CableModeToggle.prefab in the main
// menu Settings panel. The checkbox graphics/placement live in the prefab;
// no runtime cloning of an unrelated settings control is needed.
public sealed class CableModeMenu : MonoBehaviour
{
    [SerializeField] private Toggle toggle;
    [SerializeField] private Text label;

    private void Awake()
    {
        if (toggle == null) toggle = GetComponentInChildren<Toggle>(true);
        if (label == null)
        {
            var title = transform.Find("Label");
            if (title != null) label = title.GetComponent<Text>();
        }
        if (toggle != null)
        {
            toggle.SetIsOnWithoutNotify(CableMode.Selected);
            toggle.onValueChanged.AddListener(CableMode.SetSelected);
        }
        UpdateLabel();
        Localization.LanguageChanged += UpdateLabel;
    }

    private void OnDestroy()
    {
        Localization.LanguageChanged -= UpdateLabel;
        if (toggle != null) toggle.onValueChanged.RemoveListener(CableMode.SetSelected);
    }

    private void UpdateLabel()
    {
        if (label != null) label.text = Localization.GetLanguage() == "RU"
            ? "Физические провода" : "Physical cables";
    }
}
