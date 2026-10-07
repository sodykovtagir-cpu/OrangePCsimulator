using UnityEngine;

/// <summary>
/// Визуальная сетка Orange PC.
///
/// Рисует берёзовую «голограмму» сетки на той поверхности, куда ты сейчас
/// целишься (пол, стена, стол), когда включён режим сетки:
///   • линии мировой сетки (каждая N-я — жирнее),
///   • подсветка клетки, в центр которой встанет предмет,
///   • мягкое затухание к краям, чтобы не резало глаз,
///   • сетка не накладывается на перетаскиваемый предмет (окклюдер-stencil).
///
/// Координаты линий считаются по МИРОВЫМ осям, поэтому рисунок всегда
/// совпадает со снапом PlacementGrid: предмет встаёт ровно в центр
/// подсвеченной клетки.
///
/// Основной путь — шейдер OrangePC/GridOverlay (Assets/Shader).
/// Если шейдер по какой-то причине недоступен (не собрался, вырезан при
/// стриппинге) — включается запасной режим на GL.Lines, сетка всё равно видна.
/// </summary>
[AddComponentMenu("Orange PC/Grid/Placement Grid Visual")]
public class PlacementGridVisual : MonoBehaviour
{
    public static PlacementGridVisual Instance { get; private set; }

    [Header("Цвета")]
    [Tooltip("Берёзовая сетка: кремовые линии.")]
    public Color lineColor = new Color(0.95f, 0.90f, 0.78f, 1f);

    [Tooltip("Подсветка клетки под курсором — чуть светлее берёзового.")]
    public Color cellColor = new Color(1f, 0.97f, 0.86f, 1f);

    [Header("Геометрия")]
    [Tooltip("Сколько клеток рисовать в каждую сторону.")]
    public int cellsPerSide = 24;

    [Tooltip("Каждая N-я линия рисуется жирнее.")]
    public int majorEvery = 4;

    [Tooltip("Полутолщина линии в метрах.")]
    public float lineHalfWidth = 0.008f;

    [Tooltip("Минимальная толщина линии в пикселях (чтобы не пропадала вдали).")]
    public float pixelWidth = 0.9f;

    [Tooltip("Отступ от поверхности, чтобы не было з-файтинга.")]
    public float surfaceOffset = 0.012f;

    [Tooltip("На каком расстоянии искать поверхность под прицелом.")]
    public float maxDistance = 14f;

    [Header("Прозрачность")]
    [Tooltip("Прозрачность, когда предмет тащат.")]
    public float dragOpacity = 0.95f;

    [Tooltip("Прозрачность, когда просто включён режим (чтобы сетку было видно).")]
    public float idleOpacity = 0.35f;

    [Tooltip("Показывать сетку даже когда ничего не тащат.")]
    public bool showWhenIdle = true;

    public float fadeSpeed = 6f;

    [Header("Слои")]
    public LayerMask surfaceMask = ~0;

    [Tooltip("Слой, которым целится Raycast. Если 0 — берётся surfaceMask.")]
    public LayerMask aimMaskOverride = 0;

    private PlacementGrid grid;
    private Transform quad;
    private MeshFilter filter;
    private MeshRenderer quadRenderer;
    private Material material;
    private Material glMaterial;
    private bool glFallback;
    private Camera cachedCamera;
    // Невидимый box-окклюдер вокруг перетаскиваемого предмета: помечает его
    // область стенсилом, чтобы сетка через него не просвечивала.
    private Transform occluder;

    private float opacity;
    private bool hasAim;
    private Vector3 aimPoint;
    private Vector3 highlightPoint;
    private Vector3 aimNormal = Vector3.up;

    private static readonly int IdCellSize = Shader.PropertyToID("_CellSize");
    private static readonly int IdAxisU = Shader.PropertyToID("_AxisU");
    private static readonly int IdAxisV = Shader.PropertyToID("_AxisV");
    private static readonly int IdLineHalfWidth = Shader.PropertyToID("_LineHalfWidth");
    private static readonly int IdPixelWidth = Shader.PropertyToID("_PixelWidth");
    private static readonly int IdPixelScale = Shader.PropertyToID("_PixelScale");
    private static readonly int IdMajorEvery = Shader.PropertyToID("_MajorEvery");
    private static readonly int IdHighlightUV = Shader.PropertyToID("_HighlightUV");
    private static readonly int IdOpacity = Shader.PropertyToID("_Opacity");
    private static readonly int IdColor = Shader.PropertyToID("_Color");
    private static readonly int IdAccent = Shader.PropertyToID("_Accent");

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        grid = GetComponentInParent<PlacementGrid>();
        Build();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        if (material != null) Destroy(material);
        if (glMaterial != null) Destroy(glMaterial);
    }

    // ─────────────────────────────────────────────────────────────── сборка
    private void Build()
    {
        var quadGo = new GameObject("GridMesh");
        quadGo.transform.SetParent(transform, false);
        quadGo.hideFlags = HideFlags.DontSave;

        filter = quadGo.AddComponent<MeshFilter>();
        quadRenderer = quadGo.AddComponent<MeshRenderer>();

        var mesh = new Mesh();
        mesh.name = "GridQuad";
        mesh.hideFlags = HideFlags.DontSave;
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, 0f, -0.5f),
            new Vector3( 0.5f, 0f, -0.5f),
            new Vector3( 0.5f, 0f,  0.5f),
            new Vector3(-0.5f, 0f,  0.5f)
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        filter.sharedMesh = mesh;

        var shader = Shader.Find("OrangePC/GridOverlay");
        if (shader != null)
        {
            material = new Material(shader);
            material.hideFlags = HideFlags.HideAndDontSave;
            quadRenderer.sharedMaterial = material;
        }
        else
        {
            glFallback = true;
            var gl = Shader.Find("Hidden/Internal-Colored");
            if (gl != null)
            {
                glMaterial = new Material(gl);
                glMaterial.hideFlags = HideFlags.HideAndDontSave;
            }

            quadRenderer.enabled = false;
            Debug.LogWarning("[PlacementGridVisual] Шейдер OrangePC/GridOverlay не найден — сетка рисуется через GL.");
        }

        quadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        quadRenderer.receiveShadows = false;
        quadRenderer.enabled = !glFallback;

        quad = quadGo.transform;
        quadGo.SetActive(false);

        BuildOccluder();
    }

    // ──────────────────────────────────────────────────────── окклюдер предмета
    /// <summary>
    /// Невидимый box-окклюдер вокруг перетаскиваемого предмета (по габаритам
    /// всех его рендереров). Шейдер OrangePC/GridOccluder рисует его перед
    /// сеткой, ничего не закрашивает и не пишет глубину, а только помечает
    /// область стенсилом (Ref 57). Шейдер сетки не рисуется в помеченной
    /// области — поэтому сетка не накладывается на предмет, который тащат:
    /// ни на прозрачные части (стекло), ни там, где она ближе к камере.
    /// </summary>
    private void BuildOccluder()
    {
        var shader = Shader.Find("OrangePC/GridOccluder");
        if (shader == null)
        {
            // Без шейдера окклюдера сетка работает как раньше (с наложением).
            return;
        }

        var go = new GameObject("GridOccluder");
        go.transform.SetParent(transform, false);
        go.hideFlags = HideFlags.DontSave;
        go.layer = 2; // Ignore Raycast (коллайдера нет, но на всякий случай)

        var filt = go.AddComponent<MeshFilter>();
        filt.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");

        var rend = go.AddComponent<MeshRenderer>();
        rend.sharedMaterial = new Material(shader);
        rend.sharedMaterial.hideFlags = HideFlags.HideAndDontSave;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;

        occluder = go.transform;
        go.SetActive(false);
    }

    /// <summary>Двигает окклюдер за перетаскиваемым предметом (или выключает).</summary>
    private void UpdateOccluder()
    {
        if (occluder == null) return;

        var target = grid != null ? grid.DragTarget : null;
        bool show = target != null
            && quad != null
            && quad.gameObject.activeSelf;

        if (show)
        {
            var rends = target.GetComponentsInChildren<Renderer>();
            var b = new Bounds();
            bool has = false;

            for (int i = 0; i < rends.Length; i++)
            {
                var r = rends[i];
                if (r == null || !r.enabled) continue;

                if (!has)
                {
                    b = r.bounds;
                    has = true;
                }
                else
                {
                    b.Encapsulate(r.bounds);
                }
            }

            if (has)
            {
                // Минимальный зазор, чтобы box гарантированно накрывал предмет.
                Vector3 size = b.size;
                size.x = Mathf.Max(size.x, 0.02f);
                size.y = Mathf.Max(size.y, 0.02f);
                size.z = Mathf.Max(size.z, 0.02f);

                occluder.position = b.center;
                occluder.rotation = Quaternion.identity;
                occluder.localScale = size;

                if (!occluder.gameObject.activeSelf)
                    occluder.gameObject.SetActive(true);

                return;
            }
        }

        if (occluder.gameObject.activeSelf)
            occluder.SetActive(false);
    }

    // ────────────────────────────────────────────────────────────── управление
    /// <summary>Целимся в поверхность: сетка ложится на неё.</summary>
    public void SetAim(Vector3 point, Vector3 normal)
    {
        SetAim(point, normal, point);
    }

    /// <summary>
    /// Целимся в поверхность (на ней лежит сетка), но подсвечиваем клетку,
    /// в которой окажется предмет (snapPoint — уже отснапанная точка захвата).
    /// </summary>
    public void SetAim(Vector3 planePoint, Vector3 normal, Vector3 snapPoint)
    {
        aimPoint = planePoint;
        highlightPoint = snapPoint;
        aimNormal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
        hasAim = true;
    }

    public void ClearAim()
    {
        hasAim = false;
    }

    // ───────────────────────────────────────────────────────────────── кадр
    private void LateUpdate()
    {
        if (grid == null)
            return;

        if (!grid.SnapEnabled)
        {
            Hide();
        }
        else if (!grid.IsDragging && !showWhenIdle)
        {
            Hide();
        }
        else
        {
            if (!grid.IsDragging)
                UpdateIdleAim();

            if (!hasAim)
                Hide();
            else
                Apply();
        }

        // Окклюдер обновляется всегда: пока тащат предмет — он следует за ним,
        // в остальных случаях выключен (Hide() мог только что скрыть сетку).
        UpdateOccluder();
    }

    private void Hide()
    {
        if (opacity <= 0f && quad != null && !quad.gameObject.activeSelf)
            return;

        opacity = Mathf.MoveTowards(opacity, 0f, fadeSpeed * 2f * Time.unscaledDeltaTime);

        if (quad != null && opacity <= 0.001f)
        {
            quad.gameObject.SetActive(false);
            opacity = 0f;
        }
        else if (material != null)
        {
            material.SetFloat(IdOpacity, opacity);
        }
    }

    private void UpdateIdleAim()
    {
        var cam = GetCamera();
        if (cam == null)
        {
            hasAim = false;
            return;
        }

        // Центр экрана = прицел игрока (и на ПК, и на мобильной — как в Raycast)
        var ray = new Ray(cam.transform.position, cam.transform.forward);

        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, maxDistance, AimMask()))
        {
            SetAim(hit.point, hit.normal);
        }
        else
        {
            hasAim = false;
        }
    }

    private void Apply()
    {
        float cell = grid != null ? Mathf.Max(grid.cellSize, 0.01f) : 0.5f;

        Vector3 n = aimNormal;
        if (n.sqrMagnitude < 0.0001f) n = Vector3.up;

        // Базис плоскости: U и V — две оси вдоль поверхности, n — нормаль.
        Vector3 axisV = Mathf.Abs(n.y) > 0.9f ? new Vector3(0f, 0f, 1f) : new Vector3(0f, 1f, 0f);
        Vector3 axisU = Vector3.Cross(n, axisV);
        if (axisU.sqrMagnitude < 0.0001f) axisU = new Vector3(1f, 0f, 0f);
        axisU.Normalize();
        axisV = Vector3.Cross(axisU, n);
        if (axisV.sqrMagnitude < 0.0001f) axisV = Vector3.Cross(axisU, Vector3.up);
        axisV.Normalize();

        // Центр квада снапаем в плоскости — тогда сетка не дрожит,
        // а подсвеченная клетка всегда совпадает с точкой снапа предмета.
        float u = Vector3.Dot(aimPoint, axisU);
        float v = Vector3.Dot(aimPoint, axisV);
        float su = Mathf.Round(u / cell) * cell;
        float sv = Mathf.Round(v / cell) * cell;

        // Клетка под предметом считается по его отснапанной точке захвата,
        // а не по точке попадания луча — так подсветка не врёт.
        float hu = Mathf.Round(Vector3.Dot(highlightPoint, axisU) / cell) + 0.5f;
        float hv = Mathf.Round(Vector3.Dot(highlightPoint, axisV) / cell) + 0.5f;

        Vector3 center = aimPoint + (su - u) * axisU + (sv - v) * axisV + n * surfaceOffset;
        float extent = Mathf.Max(cellsPerSide, 2) * cell;

        if (quad != null)
        {
            if (!quad.gameObject.activeSelf) quad.gameObject.SetActive(true);

            quad.position = center;

            Quaternion target = Quaternion.LookRotation(axisV, n);
            quad.rotation = Quaternion.Slerp(quad.rotation, target,
                1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));

            // В GL-режиме линии рисуются сразу в мировых единицах — масштаб не нужен.
            quad.localScale = glFallback ? Vector3.one : new Vector3(extent, 1f, extent);
        }

        float target2 = grid != null && grid.IsDragging ? dragOpacity : idleOpacity;
        opacity = Mathf.MoveTowards(opacity, target2, fadeSpeed * Time.unscaledDeltaTime);

        if (material != null)
        {
            material.SetFloat(IdCellSize, cell);
            material.SetVector(IdAxisU, new Vector4(axisU.x, axisU.y, axisU.z, 0f));
            material.SetVector(IdAxisV, new Vector4(axisV.x, axisV.y, axisV.z, 0f));
            material.SetFloat(IdLineHalfWidth, lineHalfWidth);
            material.SetFloat(IdPixelWidth, pixelWidth);
            material.SetFloat(IdPixelScale, PixelScale());
            material.SetFloat(IdMajorEvery, Mathf.Max(majorEvery, 1));
            material.SetVector(IdHighlightUV, new Vector4(hu, hv, 1f, 0.5f));
            material.SetFloat(IdOpacity, opacity);
            material.SetColor(IdColor, lineColor);
            material.SetColor(IdAccent, cellColor);
        }
    }

    // Запасной путь: линии через GL (если шейдер недоступен).
    private void OnRenderObject()
    {
        if (!glFallback || glMaterial == null || quad == null) return;
        if (!quad.gameObject.activeInHierarchy || opacity <= 0.005f) return;

        float cell = grid != null ? Mathf.Max(grid.cellSize, 0.01f) : 0.5f;
        int n = Mathf.Max(cellsPerSide, 2);
        float half = n * cell * 0.5f;

        glMaterial.SetPass(0);
        GL.PushMatrix();
        GL.MultMatrix(quad.localToWorldMatrix);
        GL.Begin(GL.LINES);
        GL.Color(new Color(lineColor.r, lineColor.g, lineColor.b, opacity));

        for (int i = 0; i <= n; i++)
        {
            float p = -half + i * cell;

            GL.Vertex(new Vector3(p, 0f, -half));
            GL.Vertex(new Vector3(p, 0f, half));

            GL.Vertex(new Vector3(-half, 0f, p));
            GL.Vertex(new Vector3(half, 0f, p));
        }

        GL.End();
        GL.PopMatrix();
    }

    // ─────────────────────────────────────────────────────────────── helpers
    private LayerMask AimMask()
    {
        if (aimMaskOverride != 0) return aimMaskOverride;
        if (surfaceMask != 0) return surfaceMask;
        return ~0;
    }

    private Camera GetCamera()
    {
        if (cachedCamera != null) return cachedCamera;

        // Камера, которой целится сам Raycast — самый надёжный источник
        if (PlacementGrid.AimCamera != null) cachedCamera = PlacementGrid.AimCamera;
        else cachedCamera = Camera.main;

        return cachedCamera;
    }

    /// <summary>Мировая величина одного пикселя на расстоянии 1 м от камеры.</summary>
    private float PixelScale()
    {
        var cam = GetCamera();
        if (cam == null) return 0.001f;

        float h = Mathf.Max(1, cam.pixelHeight);
        return 2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / h;
    }
}
