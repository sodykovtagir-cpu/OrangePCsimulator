using PC.Component.Cabling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Use the menu's own checkbox artwork instead of changing prefab colors or
// requiring every localized menu scene to receive a separately wired widget.
public sealed class CableModeMenu : MonoBehaviour
{
    private Toggle toggle;
    private Text label;

    public static void Install()
    {
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var tr in root.GetComponentsInChildren<Transform>(true))
            {
                if (tr.name != "Settings") continue;
                var content = tr.Find("Scroll View/Viewport/Content");
                if (content == null || content.Find("Physical Cables") != null) continue;
                var example = content.Find("ShowFps");
                if (example == null || example.Find("Background") == null) continue;
                var widget = Instantiate(example.gameObject, content);
                widget.name = "Physical Cables";
                var rect = widget.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -155f);
                var fps = widget.GetComponentInChildren<ShowFps>(true);
                if (fps != null) { fps.enabled = false; Destroy(fps); }
                var title = widget.transform.Find("Label");
                if (title != null)
                {
                    var localize = title.GetComponent<LocalizationText>();
                    if (localize != null) { localize.enabled = false; Destroy(localize); }
                    var animated = title.GetComponent<TextAnimation>();
                    if (animated != null) { animated.enabled = false; Destroy(animated); }
                }
                widget.AddComponent<CableModeMenu>();
                return;
            }
        Debug.LogWarning("Settings checkbox: menu content not found");
    }

    private void Awake()
    {
        toggle = GetComponentInChildren<Toggle>(true);
        label = transform.Find("Label")?.GetComponent<Text>();
        if (toggle != null)
        {
            // Cloned UI has a persistent ShowFps listener: replace the event,
            // not RemoveAllListeners (which only removes runtime listeners).
            toggle.onValueChanged = new Toggle.ToggleEvent();
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
