using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Панель стрелок сетки: четыре крупные стрелки (влево / вверх / вправо / вниз)
/// + кнопка переключения режима «перемещение / поворот» по центру.
/// Показывается, пока предмет выбран или тащат его в режиме сетки.
///
/// На ПК предмет двигается и крутится стрелками клавиатуры, панель там не
/// нужна. На телефоне панель — основной способ управлять сеткой, поэтому:
///  • размеры считаются от экрана (на телефоне кнопки крупнее во столько же
///    во сколько телефон больше типичного ПК-экрана) — в канвасе проекта нет
///    CanvasScaler, UI рисуется в «сырых» пикселях, и без этого кнопки были
///    нечитаемо мелкими на телефоне;
///  • панель — обычный элемент раскладки мобильного управления: у неё тот же
///    controlId («GridArrows»), что и у превью в меню, поэтому её можно
///    перетащить в «Настройки управления» на телефоне, и положение сохранится;
///  • создаётся с повторами: если канвас в этот момент ещё не готов, панель
///    соберётся чуть позже, а не пропадёт навсегда;
///  • если спрайт-стрелка не загрузился, кнопки всё равно собираются — с
///    символами-стрелками (раньше панель просто удалялась).
/// </summary>
public class DragRotateControls : MonoBehaviour
{
    /// <summary>controlId для раскладки мобильного управления.</summary>
    public const string LayoutId = "GridArrows";

    public static DragRotateControls Instance { get; private set; }

    private Raycast raycast;
    private GameObject panel;
    private Text modeText;

    // Зажатая кнопка стрелки повторяет шаг, пока палец на ней
    private const float HoldDelay = 0.3f;
    private const float MoveRepeatInterval = 0.14f;
    private const float RotateRepeatInterval = 0.35f;
    private bool holdActive;
    private int holdX;
    private int holdY;
    private bool holdFine;
    private float holdNextAt;

    // Платформу проверяем один раз: на ПК панель не нужна, каждый кадр
    // сканировать сцену незачем.
    private static bool platformChecked;

    /// <summary>
    /// Создаёт панель, если её ещё нет (вызывается из Raycast). На ПК панель не
    /// собирается — там стрелки клавиатуры. В редакторе собирается, если
    /// мобильный интерфейс включён (чтобы можно было проверить не на телефоне).
    /// </summary>
    public static void EnsureExists(Raycast owner)
    {
        if (Instance != null) return; // Unity-null: после смены сцены создадим заново
        if (platformChecked) return;
        if (!ShouldBuild()) return;

        var canvas = FindHudCanvas();
        if (canvas == null) return; // попробуем в следующем кадре

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
        controls.Build(canvas);
        if (controls.panel == null)
        {
            Destroy(go);
            return;
        }

        Instance = controls;
        platformChecked = true;
    }

    /// <summary>
    /// Панель — только для телефона (и для проверки в редакторе при включённом
    /// мобильном UI). На настоящем ПК её нет: там стрелки с клавиатуры.
    /// </summary>
    private static bool ShouldBuild()
    {
#if UNITY_ANDROID || UNITY_IOS
        return true;
#else
        // В редакторе/на ПК — только если мобильный интерфейс включён вручную
        // (ControlRig на ПК оставляет активным Standalone-панель).
        var all = FindObjectsOfType<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == "Mobile" && all[i].gameObject.activeInHierarchy)
                return true;
        }
        return false;
#endif
    }

    /// <summary>Показать/скрыть панель (видна, пока предмет выбран или тащат).</summary>
    public void SetVisible(bool visible)
    {
        if (panel != null && panel.activeSelf != visible)
            panel.SetActive(visible);

        if (!visible) StopHold();
        if (visible) RefreshLabel();
    }

    private void OnDisable()
    {
        StopHold();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Пока кнопка стрелки зажата, шаг повторяется (как клавиша на ПК).</summary>
    private void Update()
    {
        if (!holdActive || raycast == null) return;
        if (Time.unscaledTime < holdNextAt) return;

        raycast.ArrowInput(holdX, holdY, holdFine);
        holdNextAt = Time.unscaledTime + (raycast.ArrowMoveMode ? MoveRepeatInterval : RotateRepeatInterval);
    }

    private void StartHold(int xDir, int yDirUp, bool fine)
    {
        if (raycast == null) return;

        holdX = xDir;
        holdY = yDirUp;
        holdFine = fine;
        holdActive = true;

        raycast.ArrowInput(xDir, yDirUp, fine);
        holdNextAt = Time.unscaledTime + HoldDelay;
    }

    private void StopHold()
    {
        holdActive = false;
    }

    /// <summary>HUD-канвас игры (общий для панели стрелок и подписи режима сетки).</summary>
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

    /// <summary>
    /// Масштаб панели под экран. В канвасе проекта нет CanvasScaler, поэтому
    /// «сырые» пиксели на телефоне (1080×2340) — это физически мелкие кнопки.
    /// Считаем от длинной стороны экрана и от DPI, когда Android его отдаёт.
    /// </summary>
    public static float UiScale()
    {
        float longSide = Mathf.Max(Screen.width, Screen.height);
        float scale = longSide / 1440f; // 1440 — типичный телефон по длинной стороне

        float dpi = Screen.dpi;
        if (dpi > 1f) scale = Mathf.Max(scale, dpi / 260f);

        return Mathf.Clamp(scale, 1f, 2.4f);
    }

    private void Build(Canvas canvas)
    {
        float s = UiScale();

        var sprite = LoadArrowSprite();

        panel = new GameObject("GridArrows", typeof(RectTransform));
        panel.transform.SetParent(transform, false);
        panel.SetActive(false);

        var panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = new Vector2(1f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(1f, 0f);
        panelRect.anchoredPosition = new Vector2(-16f, 96f);
        panelRect.sizeDelta = new Vector2(300f * s, 300f * s);

        // Прозрачная подложка-ловушка для перетаскивания в настройках.
        // В игре нажатия НЕ перехватываем: иначе пустая часть панели съедала бы
        // касания мимо кнопок и мешала переносить предмет.
        var catcher = panel.AddComponent<Image>();
        catcher.color = new Color(0f, 0f, 0f, 0f);
        catcher.raycastTarget = MobileCustomizeManager.EditMode;

        // Панель — часть раскладки мобильного управления: её можно двигать
        // в «Настройки управления», положение сохранится и переживёт сцену.
        var drag = panel.AddComponent<MobileFreeDraggable>();
        drag.Init(LayoutId, canvas.transform as RectTransform);

        // Главные стрелки: в режиме перемещения — ходят по клеткам,
        // в режиме поворота — крутят. Стрелка в спрайте смотрит вверх;
        // остальные кнопки повёрнуты.
        float big = 96f * s;
        float gap = 72f * s;
        float fine = 52f * s;
        float fineGap = 132f * s;

        Wire(CreateButton(panel.transform, sprite, "ArrowLeft", -gap, 0f, 90f, big, "←"), -1, 0, false);
        Wire(CreateButton(panel.transform, sprite, "ArrowUp", 0f, gap, 0f, big, "↑"), 0, 1, false);
        Wire(CreateButton(panel.transform, sprite, "ArrowRight", gap, 0f, -90f, big, "→"), 1, 0, false);
        Wire(CreateButton(panel.transform, sprite, "ArrowDown", 0f, -gap, 180f, big, "↓"), 0, -1, false);

        // Мелкие стрелки по краям: то же, но с уменьшенным шагом
        // (аналог Ctrl на ПК).
        Wire(CreateButton(panel.transform, sprite, "FineLeft", -fineGap, 0f, 90f, fine, "←"), -1, 0, true);
        Wire(CreateButton(panel.transform, sprite, "FineUp", 0f, fineGap, 0f, fine, "↑"), 0, 1, true);
        Wire(CreateButton(panel.transform, sprite, "FineRight", fineGap, 0f, -90f, fine, "→"), 1, 0, true);
        Wire(CreateButton(panel.transform, sprite, "FineDown", 0f, -fineGap, 180f, fine, "↓"), 0, -1, true);

        // Центральная кнопка — переключатель режима стрелок:
        // перемещение / поворот (аналог R на ПК).
        float toggleSize = 76f * s;
        var tgo = new GameObject("ModeToggle", typeof(RectTransform));
        tgo.transform.SetParent(panel.transform, false);

        var trect = (RectTransform)tgo.transform;
        trect.anchorMin = new Vector2(0.5f, 0.5f);
        trect.anchorMax = new Vector2(0.5f, 0.5f);
        trect.pivot = new Vector2(0.5f, 0.5f);
        trect.anchoredPosition = Vector2.zero;
        trect.sizeDelta = new Vector2(toggleSize, toggleSize);

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
        modeText.fontSize = Mathf.RoundToInt(17f * s);
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

        bool rotate = raycast != null && !raycast.ArrowMoveMode;
        string text = Localization.GetText(rotate ? "Grid: rotate" : "Grid: move");

        // Длинные переводы в круглую кнопку не влезают — оставляем первое слово
        if (text != null && text.Length > 8)
        {
            int space = text.IndexOf(' ');
            if (space > 0 && space <= 8) text = text.Substring(0, space);
        }

        modeText.text = text;
    }

    private Button CreateButton(Transform parent, Sprite sprite, string name,
        float x, float y, float zRotation, float size, string fallbackGlyph)
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

        if (sprite != null)
        {
            var arrowGo = new GameObject("Arrow", typeof(RectTransform));
            arrowGo.transform.SetParent(go.transform, false);

            var arrowRect = (RectTransform)arrowGo.transform;
            arrowRect.anchorMin = Vector2.zero;
            arrowRect.anchorMax = Vector2.one;
            arrowRect.offsetMin = new Vector2(size * 0.18f, size * 0.18f);
            arrowRect.offsetMax = new Vector2(-size * 0.18f, -size * 0.18f);

            var arrow = arrowGo.AddComponent<Image>();
            arrow.sprite = sprite;
            arrow.color = Color.white;
            arrow.preserveAspect = true;
        }
        else
        {
            // Спрайта нет — рисуем символом, кнопка всё равно должна работать.
            var glyphGo = new GameObject("Glyph", typeof(RectTransform));
            glyphGo.transform.SetParent(go.transform, false);

            var glyphRect = (RectTransform)glyphGo.transform;
            glyphRect.anchorMin = Vector2.zero;
            glyphRect.anchorMax = Vector2.one;
            glyphRect.offsetMin = Vector2.zero;
            glyphRect.offsetMax = Vector2.zero;

            var text = glyphGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = fallbackGlyph;
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = Mathf.RoundToInt(size * 0.6f);
            text.color = Color.white;
        }

        var button = go.AddComponent<Button>();
        button.targetGraphic = bg;
        return button;
    }

    private void Wire(Button button, int xDir, int yDirUp, bool fine)
    {
        if (button == null) return;

        // Кнопку можно зажать: первый шаг — по нажатию, дальше повтор, пока палец на
        // ней. onClick не используем: отпускание дало бы ещё один лишний шаг.
        var trigger = button.gameObject.AddComponent<EventTrigger>();
        AddPointerEntry(trigger, EventTriggerType.PointerDown, () => StartHold(xDir, yDirUp, fine));
        AddPointerEntry(trigger, EventTriggerType.PointerUp, StopHold);
        AddPointerEntry(trigger, EventTriggerType.PointerExit, StopHold);
    }

    private static void AddPointerEntry(EventTrigger trigger, EventTriggerType type, System.Action action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => action());
        trigger.triggers.Add(entry);
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

    // ─────────────────────────────────────── превью для настроек управления

    /// <summary>
    /// Собирает копию панели для экрана настройки мобильного управления (меню).
    /// Копия только показывается и перетаскивается — в игре она не работает,
    /// но имеет тот же controlId, поэтому сохранённую позицию подхватит
    /// настоящая панель.
    /// </summary>
    public static GameObject BuildPreview(RectTransform parent)
    {
        if (parent == null) return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            var existing = child.GetComponent<MobileFreeDraggable>();
            if (existing != null && existing.ControlId == LayoutId)
                return child.gameObject;
        }

        float s = UiScale();
        var sprite = LoadArrowSprite();

        var panel = new GameObject("GridArrows", typeof(RectTransform));
        panel.transform.SetParent(parent, false);

        var rect = (RectTransform)panel.transform;
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-16f, 96f);
        rect.sizeDelta = new Vector2(300f * s, 300f * s);

        float big = 96f * s;
        float gap = 72f * s;
        float fine = 52f * s;
        float fineGap = 132f * s;

        // В настройках панель целиком тянется мышью/пальцем за любую точку.
        var catcher = panel.AddComponent<Image>();
        catcher.color = new Color(0f, 0f, 0f, 0f);
        catcher.raycastTarget = true;

        AddPreviewArrow(panel.transform, sprite, "ArrowLeft", -gap, 0f, 90f, big, "←");
        AddPreviewArrow(panel.transform, sprite, "ArrowUp", 0f, gap, 0f, big, "↑");
        AddPreviewArrow(panel.transform, sprite, "ArrowRight", gap, 0f, -90f, big, "→");
        AddPreviewArrow(panel.transform, sprite, "ArrowDown", 0f, -gap, 180f, big, "↓");
        AddPreviewArrow(panel.transform, sprite, "FineLeft", -fineGap, 0f, 90f, fine, "←");
        AddPreviewArrow(panel.transform, sprite, "FineUp", 0f, fineGap, 0f, fine, "↑");
        AddPreviewArrow(panel.transform, sprite, "FineRight", fineGap, 0f, -90f, fine, "→");
        AddPreviewArrow(panel.transform, sprite, "FineDown", 0f, -fineGap, 180f, fine, "↓");

        var tgo = new GameObject("ModeToggle", typeof(RectTransform));
        tgo.transform.SetParent(panel.transform, false);
        var trect = (RectTransform)tgo.transform;
        trect.anchorMin = new Vector2(0.5f, 0.5f);
        trect.anchorMax = new Vector2(0.5f, 0.5f);
        trect.pivot = new Vector2(0.5f, 0.5f);
        trect.anchoredPosition = Vector2.zero;
        trect.sizeDelta = new Vector2(76f * s, 76f * s);

        var tbg = tgo.AddComponent<Image>();
        tbg.color = new Color(0f, 0f, 0f, 0.6f);

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(tgo.transform, false);
        var lrect = (RectTransform)labelGo.transform;
        lrect.anchorMin = Vector2.zero;
        lrect.anchorMax = Vector2.one;
        lrect.offsetMin = Vector2.zero;
        lrect.offsetMax = Vector2.zero;

        var text = labelGo.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = Localization.GetText("Grid: move");
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = Mathf.RoundToInt(17f * s);
        text.color = Color.white;

        var drag = panel.AddComponent<MobileFreeDraggable>();
        drag.Init(LayoutId, parent);

        return panel;
    }

    /// <summary>
    /// Перехват кликов превью-панели — только в режиме редактирования раскладки.
    /// Иначе невидимая панель размером с палец съедала бы нажатия в меню.
    /// </summary>
    public static void SetPreviewCatcher(RectTransform root, bool catchClicks)
    {
        if (root == null) return;

        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (child.name != "GridArrows") continue;

            var img = child.GetComponent<Image>();
            if (img != null) img.raycastTarget = catchClicks;

            // Кнопки-стрелки превью — обычные картинки без логики
            for (int j = 0; j < child.childCount; j++)
            {
                var arrowImg = child.GetChild(j).GetComponent<Image>();
                if (arrowImg != null) arrowImg.raycastTarget = catchClicks;
            }
        }
    }

    private static void AddPreviewArrow(Transform parent, Sprite sprite, string name,
        float x, float y, float zRotation, float size, string glyph)
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

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.45f);

        if (sprite != null)
        {
            var arrowGo = new GameObject("Arrow", typeof(RectTransform));
            arrowGo.transform.SetParent(go.transform, false);
            var arrowRect = (RectTransform)arrowGo.transform;
            arrowRect.anchorMin = Vector2.zero;
            arrowRect.anchorMax = Vector2.one;
            arrowRect.offsetMin = new Vector2(size * 0.18f, size * 0.18f);
            arrowRect.offsetMax = new Vector2(-size * 0.18f, -size * 0.18f);

            var arrow = arrowGo.AddComponent<Image>();
            arrow.sprite = sprite;
            arrow.color = Color.white;
            arrow.preserveAspect = true;
        }
        else
        {
            var glyphGo = new GameObject("Glyph", typeof(RectTransform));
            glyphGo.transform.SetParent(go.transform, false);
            var glyphRect = (RectTransform)glyphGo.transform;
            glyphRect.anchorMin = Vector2.zero;
            glyphRect.anchorMax = Vector2.one;
            glyphRect.offsetMin = Vector2.zero;
            glyphRect.offsetMax = Vector2.zero;

            var text = glyphGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = glyph;
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = Mathf.RoundToInt(size * 0.6f);
            text.color = Color.white;
        }
    }
}