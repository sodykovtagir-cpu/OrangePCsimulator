using System.Collections;
using UnityEngine;

public class MobileCustomizeManager : MonoBehaviour
{
    public static bool EditMode { get; private set; }

    [Header("Превью раскладки, которое двигаем")]
    [SerializeField]
    private GameObject previewPanel;

    [Header("Root, внутри которого лежат draggable preview-кнопки")]
    [SerializeField]
    private RectTransform controlsRoot;

    [Header("Обычная панель: Редактировать / Назад")]
    [SerializeField]
    private GameObject normalPanel;

    [Header("Панель редактирования: Сохранить / Отмена / Reset")]
    [SerializeField]
    private GameObject editPanel;

    private Vector2 lastPreviewArea;
    private int lastScreenWidth, lastScreenHeight;

    private MobileFreeDraggable[] controls;

    private MobileAdaptiveSlidePanel[] adaptivePanels;

    private void Awake()
    {
        if (controlsRoot == null && previewPanel != null)
            controlsRoot = previewPanel.transform as RectTransform;

        FitPreviewToScreen();
        RefreshControls();
        SetEditMode(false);
    }

    private void OnEnable()
    {
        if (controlsRoot == null && previewPanel != null)
            controlsRoot = previewPanel.transform as RectTransform;

        if (previewPanel != null)
            previewPanel.SetActive(true);

        SetEditMode(false);

        StartCoroutine(ApplySavedLayoutAfterStart());
    }

    private void Start()
    {
        StartCoroutine(ApplySavedLayoutAfterStart());
    }

    private void OnDisable()
    {
        // EditMode is static across scene loads. Leaving the settings menu
        // while editing must not disable every mobile HUD button in a room.
        EditMode = false;
    }

    private IEnumerator ApplySavedLayoutAfterStart()
    {
        yield return null;
        ApplySavedLayout();

        yield return new WaitForEndOfFrame();
        ApplySavedLayout();
    }

    // The menu is a world-space 840x500 canvas, not the phone screen.
    // Match the room HUD's Expand CanvasScaler (reference 800x500), then
    // uniformly fit that logical viewport inside the authored menu shell.
    private void FitPreviewToScreen()
    {
        if (previewPanel == null) return;
        var viewport = previewPanel.transform as RectTransform;
        var area = viewport != null ? viewport.parent as RectTransform : null;
        if (area == null || Screen.width <= 0 || Screen.height <= 0) return;
        float aspect = (float)Screen.width / Screen.height;
        Vector2 logical = aspect >= 800f / 500f
            ? new Vector2(500f * aspect, 500f)
            : new Vector2(800f, 800f / aspect);
        float fit = Mathf.Min(area.rect.width / logical.x, area.rect.height / logical.y);
        if (fit <= 0f) return;
        viewport.anchorMin = viewport.anchorMax = new Vector2(0.5f, 0.5f);
        viewport.pivot = new Vector2(0.5f, 0.5f);
        viewport.sizeDelta = logical;
        viewport.anchoredPosition = Vector2.zero;
        viewport.localScale = new Vector3(fit, fit, 1f);
        lastPreviewArea = area.rect.size;
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
    }

    private void LateUpdate()
    {
        var viewport = previewPanel != null ? previewPanel.transform as RectTransform : null;
        var area = viewport != null ? viewport.parent as RectTransform : null;
        if (area != null && (lastPreviewArea != area.rect.size ||
            lastScreenWidth != Screen.width || lastScreenHeight != Screen.height))
            FitPreviewToScreen(); // Never reload prefs while the user is dragging.
    }

    private void RefreshControls()
    {
        if (controlsRoot == null)
        {
            Debug.LogError("MobileCustomizeManager: Controls Root не указан.");
            controls = new MobileFreeDraggable[0];
            adaptivePanels = new MobileAdaptiveSlidePanel[0];
            return;
        }

        // Стрелки сетки (перемещение/поворот) — тоже элемент раскладки: в игре
        // панель создаётся кодом, здесь кладём её копию-превью, чтобы её можно
        // было двигать и сохранять вместе с остальным управлением.
        DragRotateControls.BuildPreview(controlsRoot);

        controls = controlsRoot.GetComponentsInChildren<MobileFreeDraggable>(true);
        adaptivePanels = controlsRoot.GetComponentsInChildren<MobileAdaptiveSlidePanel>(true);
    }

    public void BeginEdit()
    {
        Debug.Log("Mobile edit mode ON");

        if (previewPanel != null)
            previewPanel.SetActive(true);

        RefreshControls();

        // Не вызываем ApplySavedLayout тут, чтобы не откатить уже передвинутые элементы.
        SetEditMode(true);
    }

    public void SaveAndClose()
    {
        Debug.Log("Mobile layout save");

        RefreshControls();
        SaveLayout();

        SetEditMode(false);

        // Превью оставляем видимым.
        if (previewPanel != null)
            previewPanel.SetActive(true);
    }

    public void CancelEdit()
    {
        Debug.Log("Mobile edit cancel");

        RefreshControls();
        ApplySavedLayout();

        SetEditMode(false);

        if (previewPanel != null)
            previewPanel.SetActive(true);
    }

    public void ResetLayout()
    {
        Debug.Log("Mobile layout reset");

        RefreshControls();

        foreach (var control in controls)
        {
            if (control != null)
                control.ResetPosition();
        }

        foreach (var panel in adaptivePanels)
        {
            if (panel != null)
                panel.ResetPosition();
        }

        PlayerPrefs.Save();
    }

    public void SaveLayout()
    {
        RefreshControls();

        foreach (var control in controls)
        {
            if (control != null)
                control.SavePosition();
        }

        foreach (var panel in adaptivePanels)
        {
            if (panel != null)
                panel.SavePosition();
        }

        PlayerPrefs.Save();
    }

    public void ApplySavedLayout()
    {
        RefreshControls();

        foreach (var control in controls)
        {
            if (control != null)
                control.LoadPosition();
        }

        foreach (var panel in adaptivePanels)
        {
            if (panel != null)
                panel.LoadPosition();
        }
    }

    private void SetEditMode(bool value)
    {
        EditMode = value;

        if (normalPanel != null)
            normalPanel.SetActive(!value);

        if (editPanel != null)
            editPanel.SetActive(value);

        // Панель стрелок сетки ловит нажатия только в режиме редактирования:
        // иначе невидимый прямоугольник перехватывал бы клики по меню.
        if (controlsRoot != null)
            DragRotateControls.SetPreviewCatcher(controlsRoot, value);

        // ВАЖНО:
        // previewPanel не выключаем тут.
        // Она должна быть видна и в обычном режиме, и в режиме редактирования.
    }
}