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

    private MobileFreeDraggable[] controls;

    private MobileAdaptiveSlidePanel[] adaptivePanels;

    private void Awake()
    {
        if (controlsRoot == null && previewPanel != null)
            controlsRoot = previewPanel.transform as RectTransform;

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

    private IEnumerator ApplySavedLayoutAfterStart()
    {
        yield return null;
        ApplySavedLayout();

        yield return new WaitForEndOfFrame();
        ApplySavedLayout();
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

        // ВАЖНО:
        // previewPanel не выключаем тут.
        // Она должна быть видна и в обычном режиме, и в режиме редактирования.
    }
}