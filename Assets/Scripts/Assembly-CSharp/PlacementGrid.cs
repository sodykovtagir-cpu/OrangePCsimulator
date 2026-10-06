using System;
using UnityEngine;

/// <summary>
/// Сетка размещения предметов Orange PC (снап + визуал).
///
/// Включается кнопкой в хотбаре или клавишей (по умолчанию G, настраивается
/// в Menu -> Settings -> PC controls -> Grid).
///
/// Что делает:
///  • снапает точку захвата (spring) к ближайшей клетке мировой сетки,
///    поэтому предмет сам «ходит» по клеткам;
///  • показывает видимую оранжевую сетку (PlacementGridVisual) на поверхности
///    под прицелом и подсвечивает клетку, куда предмет встанет.
///
/// Защиты:
///  • ПК не разваливается: при переносе корпуса фризим внутренние констрейнты;
///  • стены: сетка мировая (от 0,0,0), а не от объекта — до стены дотягивается;
///  • лифт: если держишь предмет на лифте — не трясёт и не проваливается;
///  • склоны: на крутых склонах предмет не едет.
/// </summary>
public class PlacementGrid : MonoBehaviour
{
    public static PlacementGrid Instance { get; private set; }

    /// <summary>Слой, которым целится Raycast (ставится им же в Start).</summary>
    public static LayerMask AimLayer;

    /// <summary>Камера игрока, которой целится Raycast (ставится им же в Start).</summary>
    public static Camera AimCamera;

    /// <summary>Вызывается при вкл/выкл режима сетки (обновляем иконку хотбара).</summary>
    public static event Action<bool> StateChanged;

    [Header("Настройки сетки")]
    [Tooltip("Размер клетки в метрах. 0.5 = полметра.")]
    public float cellSize = 0.5f;

    [Tooltip("Снапать высоту. Выключено — предмет не прыгает по Y на столах/полках.")]
    public bool snapHeight = false;

    [Tooltip("Снап включён по умолчанию?")]
    public bool enabledByDefault = false;

    [Header("Вид сетки")]
    public bool showVisual = true;

    public Color lineColor = new Color(0.68f, 0.68f, 0.70f, 1f);

    public Color cellColor = new Color(0.95f, 0.95f, 0.97f, 1f);

    [Tooltip("Сколько клеток рисовать в каждую сторону.")]
    public int cellsPerSide = 24;

    [Tooltip("Каждая N-я линия жирнее.")]
    public int majorEvery = 4;

    [Tooltip("Полутолщина линии в метрах.")]
    public float lineHalfWidth = 0.008f;

    [Tooltip("Отступ сетки от поверхности (против з-файтинга).")]
    public float surfaceOffset = 0.012f;

    [Tooltip("Прозрачность при перетаскивании.")]
    public float dragOpacity = 0.95f;

    [Tooltip("Прозрачность когда просто включён режим (сетку видно сразу).")]
    public float idleOpacity = 0.35f;

    [Tooltip("Показывать сетку, даже когда ничего не тащат.")]
    public bool showWhenIdle = true;

    [Tooltip("На каком расстоянии искать поверхность под прицелом.")]
    public float maxDistance = 14f;

    [Tooltip("Слои поверхностей для визуала. Если пусто — любые.")]
    public LayerMask surfaceMask = ~0;

    private const string PrefKey = "PlacementGrid_Enabled";
    private const float MaxSlopeAngle = 45f;

    private PlacementGridVisual visual;
    private Rigidbody[] pcBodies;
    private RigidbodyConstraints[] pcOldConstraints;
    private bool dragging;

    public bool SnapEnabled { get; private set; }

    /// <summary>Игрок сейчас тащит предмет (сетка рисуется ярче).</summary>
    public bool IsDragging
    {
        get { return dragging; }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        SnapEnabled = PlayerPrefs.GetInt(PrefKey, enabledByDefault ? 1 : 0) == 1;

        CreateVisual();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void CreateVisual()
    {
        if (!showVisual) return;
        if (visual != null) return;

        var go = new GameObject("GridVisual");
        go.transform.SetParent(transform, false);
        go.hideFlags = HideFlags.DontSave;

        visual = go.AddComponent<PlacementGridVisual>();
        visual.lineColor = lineColor;
        visual.cellColor = cellColor;
        visual.cellsPerSide = cellsPerSide;
        visual.majorEvery = majorEvery;
        visual.lineHalfWidth = lineHalfWidth;
        visual.surfaceOffset = surfaceOffset;
        visual.dragOpacity = dragOpacity;
        visual.idleOpacity = idleOpacity;
        visual.showWhenIdle = showWhenIdle;
        visual.maxDistance = maxDistance;
        visual.surfaceMask = surfaceMask;
        visual.aimMaskOverride = AimLayer;
    }

    // ─────────────────────────────────────────────────────────── вкл / выкл
    public void Toggle()
    {
        SetEnabled(!SnapEnabled);
    }

    public void SetEnabled(bool on)
    {
        SnapEnabled = on;

        PlayerPrefs.SetInt(PrefKey, on ? 1 : 0);
        PlayerPrefs.Save();

        if (visual != null) visual.aimMaskOverride = AimLayer;

        if (!on && visual != null) visual.ClearAim();

        RefreshHotbar();

        string msg = Localization.GetText(on ? "Grid: ON" : "Grid: OFF");

        var main = Main.Instance;
        if (main != null) main.FadeText(msg);

        var handler = StateChanged;
        if (handler != null) handler(on);

        Debug.Log("[PlacementGrid] " + msg + " (клетка " + cellSize + " м)");
    }

    private void RefreshHotbar()
    {
        var all = FindObjectsOfType<Functions>();
        if (all == null) return;

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null) all[i].RefreshGridIcon();
        }
    }

    // ─────────────────────────────────────────────────────────────── прицел
    /// <summary>Raycast сообщает точку и нормаль поверхности под прицелом.</summary>
    public void ReportAim(Vector3 point, Vector3 normal)
    {
        if (visual != null) visual.SetAim(point, normal);
    }

    /// <summary>
    /// Raycast сообщает поверхность (на ней лежит сетка) и уже отснапанную
    /// точку захвата — по ней подсвечивается клетка, куда встанет предмет.
    /// </summary>
    public void ReportAim(Vector3 surfacePoint, Vector3 normal, Vector3 snapPoint)
    {
        if (visual != null) visual.SetAim(surfacePoint, normal, snapPoint);
    }

    // ───────────────────────────────────────────────────────────────── снап
    /// <summary>
    /// Снапает мировую точку к сетке с учётом нормали поверхности.
    /// hitNormal — нормаль того, куда целишься, hitCollider — для лифта/склона.
    /// </summary>
    public Vector3 SnapPosition(Vector3 worldPos, Vector3 hitNormal, Collider hitCollider)
    {
        if (!SnapEnabled) return worldPos;

        if (hitCollider != null)
        {
            // Лифт: по высоте не снапаем, чтобы не дёргало.
            if (IsElevator(hitCollider))
            {
                return new Vector3(
                    SnapAxis(worldPos.x),
                    worldPos.y,
                    SnapAxis(worldPos.z));
            }

            float slope = Vector3.Angle(hitNormal, Vector3.up);
            if (slope > MaxSlopeAngle && slope < 135f)
                return SnapOnWall(worldPos, hitNormal);
        }

        if (Mathf.Abs(hitNormal.y) < 0.7f)
            return SnapOnWall(worldPos, hitNormal);

        // Пол / стол / полка
        return new Vector3(
            SnapAxis(worldPos.x),
            snapHeight ? SnapAxis(worldPos.y) : worldPos.y,
            SnapAxis(worldPos.z));
    }

    private Vector3 SnapOnWall(Vector3 pos, Vector3 normal)
    {
        if (Mathf.Abs(normal.x) > 0.7f)
        {
            return new Vector3(pos.x, SnapAxis(pos.y), SnapAxis(pos.z));
        }

        if (Mathf.Abs(normal.z) > 0.7f)
        {
            return new Vector3(SnapAxis(pos.x), SnapAxis(pos.y), pos.z);
        }

        return new Vector3(SnapAxis(pos.x), pos.y, SnapAxis(pos.z));
    }

    private float SnapAxis(float v)
    {
        float cell = Mathf.Max(cellSize, 0.01f);
        return Mathf.Round(v / cell) * cell;
    }

    private bool IsElevator(Collider col)
    {
        if (col == null) return false;

        string tag = col.tag;
        if (tag == "Elevator" || tag == "Lift" || tag == "MovingPlatform") return true;

        var rb = col.attachedRigidbody;
        if (rb != null && rb.isKinematic && rb.velocity.sqrMagnitude > 0.01f) return true;

        var parent = col.GetComponentInParent<MonoBehaviour>();
        if (parent != null)
        {
            string name = parent.GetType().Name;
            if (name.Contains("Elevator") || name.Contains("Lift")) return true;
        }

        return false;
    }

    // ─────────────────────────────────────────────────────── защита при переносе
    public void OnDragStarted(Transform target)
    {
        dragging = true;

        if (!SnapEnabled) return;
        if (target == null) return;

        var pcCase = target.GetComponentInParent<PC.Component.Case>();
        if (pcCase == null) pcCase = target.GetComponent<PC.Component.Case>();

        if (pcCase != null)
        {
            pcBodies = pcCase.GetComponentsInChildren<Rigidbody>(true);
            if (pcBodies != null && pcBodies.Length > 0)
            {
                pcOldConstraints = new RigidbodyConstraints[pcBodies.Length];
                for (int i = 0; i < pcBodies.Length; i++)
                {
                    if (pcBodies[i] == null) continue;

                    pcOldConstraints[i] = pcBodies[i].constraints;
                    pcBodies[i].constraints |= RigidbodyConstraints.FreezeRotation;
                    pcBodies[i].drag = Mathf.Max(pcBodies[i].drag, 5f);
                    pcBodies[i].angularDrag = Mathf.Max(pcBodies[i].angularDrag, 5f);
                }
            }
        }
        else
        {
            var rb = target.GetComponentInParent<Rigidbody>();
            if (rb != null)
            {
                pcBodies = new[] { rb };
                pcOldConstraints = new[] { rb.constraints };
                rb.constraints |= RigidbodyConstraints.FreezeRotation;
                rb.drag = Mathf.Max(rb.drag, 4f);
                rb.angularDrag = Mathf.Max(rb.angularDrag, 4f);
            }
        }

        if (visual != null) visual.aimMaskOverride = AimLayer;
    }

    public void OnDragEnded()
    {
        dragging = false;

        if (pcBodies != null)
        {
            for (int i = 0; i < pcBodies.Length; i++)
            {
                if (pcBodies[i] == null) continue;
                if (pcOldConstraints != null && i < pcOldConstraints.Length)
                    pcBodies[i].constraints = pcOldConstraints[i];
            }

            pcBodies = null;
            pcOldConstraints = null;
        }

        if (visual != null) visual.ClearAim();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!SnapEnabled) return;

        Gizmos.color = new Color(1f, 0.55f, 0.14f, 0.25f);

        Vector3 origin = transform.position;
        float cell = Mathf.Max(cellSize, 0.01f);

        for (int x = -10; x <= 10; x++)
        {
            Vector3 a = origin + new Vector3(x * cell, 0f, -10f * cell);
            Vector3 b = origin + new Vector3(x * cell, 0f, 10f * cell);
            Gizmos.DrawLine(a, b);
        }

        for (int z = -10; z <= 10; z++)
        {
            Vector3 a = origin + new Vector3(-10f * cell, 0f, z * cell);
            Vector3 b = origin + new Vector3(10f * cell, 0f, z * cell);
            Gizmos.DrawLine(a, b);
        }
    }
#endif
}
