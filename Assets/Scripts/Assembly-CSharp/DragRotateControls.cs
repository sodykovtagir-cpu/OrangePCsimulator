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

    /// <summary>Создаёт панель, если её ещё нет (вызывается из Raycast.Start).</summary>
    public static void EnsureExists(Raycast owner)
    {
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
    }

    /// <summary>Показать/скрыть панель (видна только при перетаскивании).</summary>
    public void SetVisible(bool visible)
    {
        if (panel != null && panel.activeSelf != visible)
            panel.SetActive(visible);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private static Canvas FindHudCanvas()
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
        panelRect.sizeDelta = new Vector2(160f, 160f);

        // Стрелка в спрайте смотрит вверх; остальные кнопки повёрнуты
        Wire(CreateButton(panel.transform, sprite, "RotateLeft", -52f, 0f, 90f), -1, 0);
        Wire(CreateButton(panel.transform, sprite, "RotateUp", 0f, 52f, 0f), 0, -1);
        Wire(CreateButton(panel.transform, sprite, "RotateRight", 52f, 0f, -90f), 1, 0);
        Wire(CreateButton(panel.transform, sprite, "RotateDown", 0f, -52f, 180f), 0, 1);
    }

    private Button CreateButton(Transform parent, Sprite sprite, string name,
        float x, float y, float zRotation)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(48f, 48f);
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

    private void Wire(Button button, int yawDir, int pitchDir)
    {
        if (button == null) return;

        button.onClick.AddListener(() =>
        {
            if (raycast != null) raycast.RotateDragged(yawDir, pitchDir);
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
