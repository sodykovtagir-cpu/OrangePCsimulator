using System;
using UnityEngine;

/// <summary>
/// Сетка размещения предметов Orange PC (снап + визуал).
///
/// Включается кнопкой в хотбаре или клавишей (по умолчанию G, настраивается
/// в Menu -> Settings -> PC controls -> Grid).
///
/// Что делает:
///  • снапает предмет к ближайшей клетке мировой сетки мгновенно (без
///    прыгучести), поэтому предмет сам «ходит» по клеткам;
///  • показывает видимую берёзовую сетку (PlacementGridVisual) на поверхности
///    под прицелом и подсвечивает клетку, куда предмет встанет.
///
/// Защиты:
///  • ПК/майнер не разваливается: весь корпус едет целиком (снап сетки и
///    поворот двигают все его тела сразу), а джойнты корпуса на время
///    переноса становятся неразрывными;
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

    [Tooltip("Снапать высоту (ось Y): предмет встаёт по сетке и вверх/вниз.")]
    public bool snapHeight = true;

    [Tooltip("Снап включён по умолчанию?")]
    public bool enabledByDefault = false;

    [Header("Вид сетки")]
    public bool showVisual = true;

    [Tooltip("Берёзовая сетка: белёсые линии с тёплым оттенком.")]
    public Color lineColor = new Color(0.97f, 0.95f, 0.86f, 1f);

    [Tooltip("Подсветка клетки под курсором — чуть светлее берёзового.")]
    public Color cellColor = new Color(1f, 0.98f, 0.92f, 1f);

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
    private Joint[] pcJoints;
    private float[] pcJointBreakForce;
    private float[] pcJointBreakTorque;
    private bool dragging;

    public bool SnapEnabled { get; private set; }

    /// <summary>Игрок сейчас тащит предмет (сетка рисуется ярче).</summary>
    public bool IsDragging
    {
        get { return dragging; }
    }

    /// <summary>
    /// Предмет, который сейчас тащат. Нужен визуалу: сетка не должна
    /// накладываться на него (PlacementGridVisual строит вокруг него окклюдер).
    /// </summary>
    public Transform DragTarget { get; private set; }

    /// <summary>
    /// Тела всего корпуса, который сейчас тащат (у ПК/майнера — все его детали,
    /// иначе — одно тело). null, когда ничего не тащат. Снап сетки и поворот
    /// двигают все эти тела разом, чтобы сборка не рассыпалась.
    /// </summary>
    public Rigidbody[] DragAssemblyBodies { get; private set; }

    /// <summary>
    /// Можно ли целиться сквозь этот коллайдер при поиске поверхности для
    /// сетки: сквозь игрока и сквозь тащимый предмет — да (сетка ложится на
    /// пол/стену за ними, а не на них самих).
    /// </summary>
    public bool IsAimBlocker(Collider col)
    {
        if (col == null) return false;

        // Игрок: сетка не должна ложиться на самого игрока
        var player = Player.Instance;
        if (player != null && col.transform.IsChildOf(player.transform)) return true;

        // Тащимый предмет (весь корпус)
        var target = DragTarget;
        if (target != null)
        {
            if (col.transform.IsChildOf(target)) return true;

            var rb = col.attachedRigidbody;
            var bodies = DragAssemblyBodies;
            if (rb != null && bodies != null)
            {
                for (int i = 0; i < bodies.Length; i++)
                {
                    if (bodies[i] == rb) return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Ищет поверхность под прицелом для сетки: ближайший коллайдер по пути,
    /// сквозь игрока и тащимый предмет (см. IsAimBlocker).
    /// </summary>
    public bool RaycastAimSurface(Ray ray, float maxDistance, LayerMask mask, out RaycastHit hit)
    {
        var hits = Physics.RaycastAll(ray, maxDistance, mask, QueryTriggerInteraction.UseGlobal);

        hit = default(RaycastHit);
        float best = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (IsAimBlocker(h.collider)) continue;
            if (h.distance < best)
            {
                best = h.distance;
                hit = h;
            }
        }

        return best < float.MaxValue;
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
        DragTarget = target;

        if (target == null) return;

        var pcCase = target.GetComponentInParent<PC.Component.Case>();
        if (pcCase == null) pcCase = target.GetComponent<PC.Component.Case>();

        if (pcCase != null)
        {
            // Весь корпус целиком: снап сетки и поворот двигают все тела
            // сразу — ПК/майнер едет как единое целое и не рассыпается.
            pcBodies = pcCase.GetComponentsInChildren<Rigidbody>(true);
            DragAssemblyBodies = pcBodies;

            // Джойнты корпуса не рвём, пока тащишь. В режиме сетки — всегда
            // (там весь корпус и так едет целиком), без сетки — только когда
            // схватили сам корпус: деталь по-прежнему можно вырвать силой.
            if (SnapEnabled || IsCaseBody(target, pcCase))
                ProtectCaseJoints(pcCase);
        }
        else
        {
            var rb = target.GetComponentInParent<Rigidbody>();
            if (rb != null)
            {
                pcBodies = new[] { rb };
                DragAssemblyBodies = pcBodies;
            }
        }

        if (visual != null) visual.aimMaskOverride = AimLayer;
    }

    public void OnDragEnded()
    {
        dragging = false;
        DragTarget = null;
        DragAssemblyBodies = null;

        RestoreCaseJoints();

        pcBodies = null;

        if (visual != null) visual.ClearAim();
    }

    /// <summary>Схватили ли сам корпус (а не деталь внутри него).</summary>
    private static bool IsCaseBody(Transform target, PC.Component.Case pcCase)
    {
        var caseBody = pcCase.GetComponent<Rigidbody>();
        if (caseBody == null) caseBody = pcCase.GetComponentInParent<Rigidbody>();
        if (caseBody == null) return false;

        var grabbed = target.GetComponentInParent<Rigidbody>();
        return grabbed == caseBody;
    }

    /// <summary>
    /// Делает джойнты корпуса неразрывными на время перетаскивания, чтобы
    /// ПК/майнер не рассыпался от рывков. Значения вернёт RestoreCaseJoints.
    /// </summary>
    private void ProtectCaseJoints(PC.Component.Case pcCase)
    {
        var joints = pcCase.GetComponentsInChildren<Joint>(true);
        if (joints == null || joints.Length == 0) return;

        pcJoints = joints;
        pcJointBreakForce = new float[joints.Length];
        pcJointBreakTorque = new float[joints.Length];

        for (int i = 0; i < joints.Length; i++)
        {
            var j = joints[i];
            if (j == null) continue;

            pcJointBreakForce[i] = j.breakForce;
            pcJointBreakTorque[i] = j.breakTorque;
            j.breakForce = Mathf.Infinity;
            j.breakTorque = Mathf.Infinity;
        }
    }

    /// <summary>Возвращает джойнтам корпуса сохранённые breakForce/breakTorque.</summary>
    private void RestoreCaseJoints()
    {
        if (pcJoints == null) return;

        for (int i = 0; i < pcJoints.Length; i++)
        {
            var j = pcJoints[i];
            if (j == null) continue;

            if (pcJointBreakForce != null && i < pcJointBreakForce.Length)
                j.breakForce = pcJointBreakForce[i];
            if (pcJointBreakTorque != null && i < pcJointBreakTorque.Length)
                j.breakTorque = pcJointBreakTorque[i];
        }

        pcJoints = null;
        pcJointBreakForce = null;
        pcJointBreakTorque = null;
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
