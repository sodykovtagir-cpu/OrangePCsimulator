using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Панель поворота перетаскиваемого предмета: четыре стрелки
/// (влево / вверх / вправо / вниз). Показывается только пока предмет тащат.
/// На ПК предмет поворачивается ещё и с клавиатуры — обычными стрелками
/// (см. Raycast.Update). Панель создаётся в рантайме и цепляется к
/// HUD-канвасу, поэтому не требует правки сцен.
/// </summary>
public class DragRotateControls : MonoBehaviour
{
    public static DragRotateControls Instance { get; private set; }

    private Raycast raycast;
    private GameObject panel;
    private Text modeText;

    /// <summary>
    /// Создаёт панель, если её ещё нет (вызывается из Raycast.Start).
    /// Только на Android: на ПК поворот идёт с клавиатуры, панель не нужна.
    /// </summary>
    public static void EnsureExists(Raycast owner)
    {
#if UNITY_ANDROID
        if (Instance != null) return; // Unity-null: после смены сцены создадим заново

        var canvas = FindHudCanvas();
        if (canvas == null) return;

        var go = new GameObject("DragRotateControls", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        go.hideFlags = HideFlags.DontSave;

        // Растягиваем корень на весь канвас, чтобы якоря панели работали от углов канваса
        var rootRect = (RectTransform)go.transform;
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        var controls = go.AddComponent<DragRotateControls>();
        controls.raycast = owner;
        controls.Build();
        Instance = controls;
#endif
    }

    /// <summary>Показать/скрыть панель (видна только при перетаскивании).</summary>
    public void SetVisible(bool visible)
    {
        if (panel != null && panel.activeSelf != visible)
            panel.SetActive(visible);

        if (visible) RefreshLabel();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>HUD-канвас игры (общий для панели поворота и подписи режима сетки).</summary>
    public static Canvas FindHudCanvas()
    {
        var functions = FindObjectOfType<Functions>();
        if (functions != null)
        {
            var canvas = functions.GetComponentInParent<Canvas>();
            if (canvas != null) return canvas;
        }

        var canvases = FindObjectsOfType<Canvas>();
        Canvas fallback = null;
        for (int i = 0; i < canvases.Length; i++)
        {
            var c = canvases[i];
            if (c == null) continue;
            if (c.renderMode == RenderMode.ScreenSpaceOverlay) return c;
            if (fallback == null) fallback = c;
        }

        return fallback;
    }

    private void Build()
    {
        var sprite = LoadArrowSprite();
        if (sprite == null)
        {
            Debug.LogWarning("[DragRotateControls] Не найден спрайт GridArrow — панель поворота не собрана.");
            Destroy(gameObject);
            return;
        }

        panel = new GameObject("RotatePanel", typeof(RectTransform));
        panel.transform.SetParent(transform, false);
        panel.SetActive(false);

        // Правый нижний угол экрана, над хотбаром
        var panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = new Vector2(1f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(1f, 0f);
        panelRect.anchoredPosition = new Vector2(-16f, 96f);
        panelRect.sizeDelta = new Vector2(240f, 208f);

        // Главные стрелки: в режиме перемещения — ходят по клеткам,
        // в режиме поворота — крутят. Стрелка в спрайте смотрит вверх;
        // остальные кнопки повёрнуты.
        Wire(CreateButton(panel.transform, sprite, "ArrowLeft", -64f, 0f, 90f, 48f), -1, 0, false);
        Wire(CreateButton(panel.transform, sprite, "ArrowUp", 0f, 56f, 0f, 48f), 0, 1, false);
        Wire(CreateButton(panel.transform, sprite, "ArrowRight", 64f, 0f, -90f, 48f), 1, 0, false);
        Wire(CreateButton(panel.transform, sprite, "ArrowDown", 0f, -56f, 180f, 48f), 0, -1, false);

        // Мелкие стрелки по краям: то же, но с уменьшенным шагом
        // (аналог Ctrl на ПК).
        Wire(CreateButton(panel.transform, sprite, "FineLeft", -104f, 0f, 90f, 30f), -1, 0, true);
        Wire(CreateButton(panel.transform, sprite, "FineUp", 0f, 94f, 0f, 30f), 0, 1, true);
        Wire(CreateButton(panel.transform, sprite, "FineRight", 104f, 0f, -90f, 30f), 1, 0, true);
        Wire(CreateButton(panel.transform, sprite, "FineDown", 0f, -94f, 180f, 30f), 0, -1, true);

        // Центральная кнопка — переключатель режима стрелок:
        // перемещение / поворот (аналог R на ПК).
        var tgo = new GameObject("ModeToggle", typeof(RectTransform));
        tgo.transform.SetParent(panel.transform, false);

        var trect = (RectTransform)tgo.transform;
        trect.anchorMin = new Vector2(0.5f, 0.5f);
        trect.anchorMax = new Vector2(0.5f, 0.5f);
        trect.pivot = new Vector2(0.5f, 0.5f);
        trect.anchoredPosition = Vector2.zero;
        trect.sizeDelta = new Vector2(52f, 52f);

        var tbg = tgo.AddComponent<Image>();
        tbg.color = new Color(0f, 0f, 0f, 0.6f);

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(tgo.transform, false);

        var lrect = (RectTransform)labelGo.transform;
        lrect.anchorMin = Vector2.zero;
        lrect.anchorMax = Vector2.one;
        lrect.offsetMin = Vector2.zero;
        lrect.offsetMax = Vector2.zero;

        modeText = labelGo.AddComponent<Text>();
        modeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        modeText.alignment = TextAnchor.MiddleCenter;
        modeText.fontSize = 13;
        modeText.color = Color.white;

        var toggle = tgo.AddComponent<Button>();
        toggle.targetGraphic = tbg;
        toggle.onClick.AddListener(() =>
        {
            if (raycast != null)
            {
                raycast.ToggleArrowMode();
                RefreshLabel();
            }
        });

        RefreshLabel();
    }

    /// <summary>Подпись центральной кнопки: текущий режим стрелок.</summary>
    private void RefreshLabel()
    {
        if (modeText == null) return;
        modeText.text = raycast != null && !raycast.ArrowMoveMode ? "поворот" : "движ.";
    }

    private Button CreateButton(Transform parent, Sprite sprite, string name,
        float x, float y, float zRotation, float size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(size, size);
        rect.localRotation = Quaternion.Euler(0f, 0f, zRotation);

        // Подложка, чтобы стрелку было видно на любом фоне
        var bg = go.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.45f);

        var arrowGo = new GameObject("Arrow", typeof(RectTransform));
        arrowGo.transform.SetParent(go.transform, false);

        var arrowRect = (RectTransform)arrowGo.transform;
        arrowRect.anchorMin = Vector2.zero;
        arrowRect.anchorMax = Vector2.one;
        arrowRect.offsetMin = new Vector2(8f, 8f);
        arrowRect.offsetMax = new Vector2(-8f, -8f);

        var arrow = arrowGo.AddComponent<Image>();
        arrow.sprite = sprite;
        arrow.color = Color.white;
        arrow.preserveAspect = true;

        var button = go.AddComponent<Button>();
        button.targetGraphic = bg;
        return button;
    }

    private void Wire(Button button, int xDir, int yDirUp, bool fine)
    {
        if (button == null) return;

        button.onClick.AddListener(() =>
        {
            if (raycast != null) raycast.ArrowInput(xDir, yDirUp, fine);
        });
    }

    private static Sprite LoadArrowSprite()
    {
        var sprite = Resources.Load<Sprite>("GridArrow");
        if (sprite != null) return sprite;

        var tex = Resources.Load<Texture2D>("GridArrow");
        if (tex == null) return null;

        return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
            new Vector2(0.5f, 0.5f), 100f);
    }
}
