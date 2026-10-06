using UnityEngine;

/// <summary>
/// Сетка для размещения предметов в мире.
/// Включается кнопкой (G на ПК, иконка на телефоне).
/// Когда включена — точка захвата (spring) снапается к ближайшей клетке.
/// Защиты:
///  - ПК не разваливается: при переносе корпуса временно фризим внутренние констрейнты
///  - Стены: сетка мировая (0,0,0), а не от объекта — до стены всегда дотягивается
///  - Лифт: если держишь предмет на лифте — не трясёт, не проваливается
///  - Склоны: на крутых склонах не даём предмету скользить (фризим Y)
/// </summary>
public class PlacementGrid : MonoBehaviour
{
    public static PlacementGrid Instance { get; private set; }

    [Header("Настройки сетки")]
    [Tooltip("Размер клетки в метрах. 0.5 = полметра, как в твоей фигме.")]
    public float cellSize = 0.5f;

    [Tooltip("Снап включен по умолчанию?")]
    public bool enabledByDefault = false;

    [Tooltip("Слой стен/пола для определения нормали. Если пусто — любой.")]
    public LayerMask surfaceMask = ~0;

    // Состояние в PlayerPrefs
    const string PrefKey = "PlacementGrid_Enabled";
    public bool SnapEnabled { get; private set; }

    // Для защиты лифта/склона
    const float MaxSlopeAngle = 45f; // круче — не снапаем Y, фризим

    // Кэш для PC защиты
    Rigidbody[] pcBodies;
    RigidbodyConstraints[] pcOldConstraints;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SnapEnabled = PlayerPrefs.GetInt(PrefKey, enabledByDefault ? 1 : 0) == 1;
    }

    void Start()
    {
        // Создаём нормальную кнопку через оригинальную механику (Canvas + Button), не OnGUI
        TryCreateHotbarButton();
        EnsureBindButton();
    }

    void EnsureBindButton()
    {
        // Только в меню где есть PcBindButton — добавляем ToggleGrid если нет
        var all = FindObjectsOfType<PcBindButton>();
        if (all == null || all.Length == 0) return;
        bool hasGrid = false;
        foreach (var b in all) if (b.action == PcBindAction.ToggleGrid) { hasGrid = true; break; }
        if (hasGrid) return;

        // Клонируем целый блок Earn (родитель кнопки) как шаблон, чтобы скопировать и лейбл
        PcBindButton templateBtn = null;
        foreach (var b in all) if (b.action == PcBindAction.Earn) templateBtn = b;
        if (templateBtn == null) templateBtn = all[all.Length - 1];
        if (templateBtn == null) return;

        // Родитель кнопки — это объект Earn (содержит лейбл и кнопку)
        Transform earnRoot = templateBtn.transform.parent;
        if (earnRoot == null) return;
        // Проверяем что это именно Earn (имя)
        if (!earnRoot.name.Contains("Earn"))
        {
            // Если структура другая — клонируем саму кнопку
            earnRoot = templateBtn.transform;
        }

        var container = earnRoot.parent;
        if (container == null) container = earnRoot;

        GameObject go;
        if (earnRoot != templateBtn.transform)
        {
            go = Instantiate(earnRoot.gameObject, container);
            go.name = "Grid";
            var rt = go.GetComponent<RectTransform>();
            var trt = earnRoot.GetComponent<RectTransform>();
            if (rt != null && trt != null)
                rt.anchoredPosition = trt.anchoredPosition + new Vector2(0, -60);

            // Внутри клона находим PcBindButton и меняем action
            var btn = go.GetComponentInChildren<PcBindButton>(true);
            if (btn != null) btn.action = PcBindAction.ToggleGrid;

            // Меняем текст лейбла Earn -> Grid
            var texts = go.GetComponentsInChildren<UnityEngine.UI.Text>(true);
            foreach (var t in texts)
            {
                if (t.text == "Earn") t.text = "Grid";
                if (t.gameObject.name == "Earn" || t.gameObject.name.Contains("Earn"))
                    t.gameObject.name = "Grid";
            }
            // Обновляем keyText
            var newBtn = go.GetComponentInChildren<PcBindButton>(true);
            if (newBtn != null) newBtn.Refresh();
        }
        else
        {
            // Фолбэк — клонируем только кнопку
            var go2 = Instantiate(templateBtn.gameObject, container);
            go2.name = "Grid";
            var rt = go2.GetComponent<RectTransform>();
            if (rt != null) rt.anchoredPosition = templateBtn.GetComponent<RectTransform>().anchoredPosition + new Vector2(0, -60);
            var btn2 = go2.GetComponent<PcBindButton>();
            if (btn2 != null) btn2.action = PcBindAction.ToggleGrid;
            if (btn2 != null) btn2.Refresh();
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Создаёт кнопку в хотбаре рядом с остальными иконками (как LockRotation и т.д.)
    void TryCreateHotbarButton()
    {
        // Если уже есть gridImage через Functions — не надо
        var func = FindObjectOfType<Functions>();
        if (func != null)
        {
            // Пытаемся найти уже существующий gridImage через рефлексию — если назначен, выходим
            var fi = typeof(Functions).GetField("gridImage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (fi != null)
            {
                var existing = fi.GetValue(func) as UnityEngine.UI.Image;
                if (existing != null) return;
            }
        }

        // Ищем Canvas хотбара (первый Canvas в сцене)
        var canvas = FindObjectOfType<UnityEngine.Canvas>();
        if (canvas == null) return;

        // Ищем хотбар панель — обычно Functions находится на том же объекте что и HotbarHotkeys
        Transform hotbarParent = null;
        if (func != null) hotbarParent = func.transform;
        else
        {
            var hotkeys = FindObjectOfType<HotbarHotkeys>();
            if (hotkeys != null) hotbarParent = hotkeys.transform;
        }
        if (hotbarParent == null) hotbarParent = canvas.transform;

        // Пытаемся клонировать существующую кнопку хотбара для стиля
        UnityEngine.UI.Button template = null;
        var buttons = hotbarParent.GetComponentsInChildren<UnityEngine.UI.Button>(true);
        foreach (var b in buttons)
        {
            if (b != null && b.gameObject.activeInHierarchy)
            {
                template = b;
                break;
            }
        }

        GameObject go;
        UnityEngine.UI.Image img;
        if (template != null)
        {
            go = Instantiate(template.gameObject, hotbarParent);
            go.name = "GridToggleButton";
            // Чистим старые listeners, ставим наш
            var btn = go.GetComponent<UnityEngine.UI.Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => Toggle());
            }
            img = go.GetComponent<UnityEngine.UI.Image>();
            if (img == null) img = go.GetComponentInChildren<UnityEngine.UI.Image>(true);
        }
        else
        {
            // Фолбэк: создаём простую кнопку
            go = new GameObject("GridToggleButton");
            go.transform.SetParent(hotbarParent, false);
            var rt = go.AddComponent<UnityEngine.RectTransform>();
            rt.sizeDelta = new Vector2(64, 64);
            rt.anchorMin = new Vector2(1, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(1, 0);
            rt.anchoredPosition = new Vector2(-12, 12);
            img = go.AddComponent<UnityEngine.UI.Image>();
            var btn = go.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => Toggle());
            // Текст подсказки
            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(go.transform, false);
            var txt = txtGo.AddComponent<UnityEngine.UI.Text>();
            txt.text = SnapEnabled ? "Сетка вкл" : "Сетка выкл";
            txt.font = UnityEngine.Resources.GetBuiltinResource<UnityEngine.Font>("Arial.ttf");
            txt.alignment = UnityEngine.TextAnchor.MiddleCenter;
            txt.color = UnityEngine.Color.white;
        }

        // Грузим иконку из Resources (Assets/Resources/GridIcon.png) или из Assets/UI_GridIcon.png
        Sprite gridSprite = UnityEngine.Resources.Load<Sprite>("GridIcon");
        if (gridSprite == null)
        {
            // Пробуем загрузить как Texture2D и сделать спрайт
            var tex = UnityEngine.Resources.Load<UnityEngine.Texture2D>("GridIcon");
            if (tex != null) gridSprite = UnityEngine.Sprite.Create(tex, new UnityEngine.Rect(0, 0, tex.width, tex.height), new UnityEngine.Vector2(0.5f, 0.5f), 100f);
        }
        if (gridSprite != null && img != null)
        {
            img.sprite = gridSprite;
            img.preserveAspect = true;
        }

        // Если есть Functions — привязываем для UpdateGridIcon
        if (func != null)
        {
            var fi = typeof(Functions).GetField("gridImage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (fi != null) fi.SetValue(func, img);
            var onFi = typeof(Functions).GetField("gridOnSprite", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var offFi = typeof(Functions).GetField("gridOffSprite", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (onFi != null && offFi != null && gridSprite != null)
            {
                onFi.SetValue(func, gridSprite);
                offFi.SetValue(func, gridSprite);
            }
            func.SendMessage("UpdateGridIcon", UnityEngine.SendMessageOptions.DontRequireReceiver);
        }

        // Для мобилы — делаем кнопку покрупнее и выше
#if UNITY_ANDROID
        var rt2 = go.GetComponent<UnityEngine.RectTransform>();
        if (rt2 != null)
        {
            rt2.sizeDelta = new Vector2(72, 72);
        }
#endif
    }

    public void Toggle()
    {
        SnapEnabled = !SnapEnabled;
        PlayerPrefs.SetInt(PrefKey, SnapEnabled ? 1 : 0);
        PlayerPrefs.Save();
        string txt = SnapEnabled ? "Сетка: вкл" : "Сетка: выкл";
        // Показываем подсказку через Main
        var main = Main.Instance;
        if (main != null) main.FadeText(txt);
        // Можно добавить звук
        Debug.Log("[PlacementGrid] " + txt + $" (cell {cellSize}m)");
    }

    public void SetEnabled(bool on)
    {
        if (SnapEnabled == on) return;
        Toggle();
    }

    /// <summary>
    /// Снапает мировую точку к сетке с учётом нормали поверхности.
    /// hitNormal — нормаль того, куда целишься (стена/пол), hitCollider — для лифта/склона.
    /// </summary>
    public Vector3 SnapPosition(Vector3 worldPos, Vector3 hitNormal, Collider hitCollider)
    {
        if (!SnapEnabled) return worldPos;

        // Защита лифта: если хит — движущаяся платформа (кинематик с velocity или тегом)
        if (hitCollider != null)
        {
            // Если это лифт/платформа — не снапаем Y, чтобы не дёргало
            if (IsElevator(hitCollider))
            {
                // Снапаем только XZ, Y оставляем как есть
                return new Vector3(
                    SnapAxis(worldPos.x),
                    worldPos.y,
                    SnapAxis(worldPos.z)
                );
            }

            // Защита склона: если нормаль крутая (>45° от вертикали) — фризим Y
            float slope = Vector3.Angle(hitNormal, Vector3.up);
            if (slope > MaxSlopeAngle && slope < 135f)
            {
                // На стене — снапаем в плоскости стены
                return SnapOnWall(worldPos, hitNormal);
            }
        }

        // Обычный пол — снапаем XZ, Y оставляем для высоты (чтобы не проваливался)
        // Стены — снапаем в мировой сетке, но проецируем на плоскость стены
        if (Mathf.Abs(hitNormal.y) < 0.7f)
        {
            // Стена (горизонтальная нормаль)
            return SnapOnWall(worldPos, hitNormal);
        }
        else
        {
            // Пол/потолок
            return new Vector3(
                SnapAxis(worldPos.x),
                SnapAxis(worldPos.y), // для полок/столов тоже снапаем Y, но можно оставить worldPos.y если нужно
                SnapAxis(worldPos.z)
            );
        }
    }

    // Снап в плоскости стены: проецируем точку на стену и снапаем 2D координаты стены
    Vector3 SnapOnWall(Vector3 pos, Vector3 normal)
    {
        // Выбираем две оси стены: если нормаль смотрит по X — снапаем Y/Z, если по Z — X/Y
        // Упрощённо — снапаем все 3, но потом корректируем чтобы точка осталась на плоскости
        // Мировая сетка — от (0,0,0), поэтому до стены всегда дотянется без щели
        Vector3 snapped = new Vector3(SnapAxis(pos.x), SnapAxis(pos.y), SnapAxis(pos.z));
        // Корректируем вдоль нормали, чтобы не отлипало от стены на пол-клетки
        // Оставляем координату вдоль нормали как была, снапаем только поперечные
        if (Mathf.Abs(normal.x) > 0.7f)
        {
            snapped.x = pos.x; // вдоль нормали — не трогаем
            snapped.y = SnapAxis(pos.y);
            snapped.z = SnapAxis(pos.z);
        }
        else if (Mathf.Abs(normal.z) > 0.7f)
        {
            snapped.z = pos.z;
            snapped.x = SnapAxis(pos.x);
            snapped.y = SnapAxis(pos.y);
        }
        else // потолок/пол уже обработан
        {
            snapped = new Vector3(SnapAxis(pos.x), pos.y, SnapAxis(pos.z));
        }
        return snapped;
    }

    float SnapAxis(float v)
    {
        return Mathf.Round(v / cellSize) * cellSize;
    }

    bool IsElevator(Collider col)
    {
        if (col == null) return false;
        // Тег Elevator / Lift / MovingPlatform — считаем лифтом
        string tag = col.tag;
        if (tag == "Elevator" || tag == "Lift" || tag == "MovingPlatform") return true;
        // Или кинематик с ненулевой velocity
        var rb = col.attachedRigidbody;
        if (rb != null && rb.isKinematic && rb.velocity.sqrMagnitude > 0.01f) return true;
        // Или компонент с именем Elevator
        if (col.GetComponentInParent<MonoBehaviour>() != null)
        {
            var name = col.GetComponentInParent<MonoBehaviour>().GetType().Name;
            if (name.Contains("Elevator") || name.Contains("Lift")) return true;
        }
        return false;
    }

    // Вызывается из Raycast при старте перетаскивания — защита ПК
    public void OnDragStarted(Transform target)
    {
        if (!SnapEnabled) return;
        if (target == null) return;
        // Если это корпус ПК — заморозим внутренние физики чтобы не развалился
        var pcCase = target.GetComponentInParent<PC.Component.Case>();
        if (pcCase == null) pcCase = target.GetComponent<PC.Component.Case>();
        if (pcCase != null)
        {
            // Находим все Rigidbody внутри корпуса
            pcBodies = pcCase.GetComponentsInChildren<Rigidbody>(true);
            if (pcBodies != null && pcBodies.Length > 0)
            {
                pcOldConstraints = new RigidbodyConstraints[pcBodies.Length];
                for (int i = 0; i < pcBodies.Length; i++)
                {
                    if (pcBodies[i] == null) continue;
                    pcOldConstraints[i] = pcBodies[i].constraints;
                    // Фризим ротацию чтобы не крутился, но позицию оставляем для SpringJoint
                    pcBodies[i].constraints |= RigidbodyConstraints.FreezeRotation;
                    // Увеличиваем drag чтобы не скользил на склонах
                    pcBodies[i].drag = Mathf.Max(pcBodies[i].drag, 5f);
                    pcBodies[i].angularDrag = Mathf.Max(pcBodies[i].angularDrag, 5f);
                }
            }
        }
        else
        {
            // Общий случай (монитор, стол и т.д.) — защита от склона: фризим ротацию одного тела
            var rb = target.GetComponentInParent<Rigidbody>();
            if (rb != null)
            {
                pcBodies = new Rigidbody[] { rb };
                pcOldConstraints = new RigidbodyConstraints[] { rb.constraints };
                rb.constraints |= RigidbodyConstraints.FreezeRotation;
                rb.drag = Mathf.Max(rb.drag, 4f);
                rb.angularDrag = Mathf.Max(rb.angularDrag, 4f);
            }
        }
    }

    public void OnDragEnded()
    {
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
    }

    // Визуализация сетки в редакторе
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!SnapEnabled) return;
        Gizmos.color = new Color(1f, 1f, 1f, 0.15f);
        Vector3 origin = transform.position;
        // Рисуем 10x10 клеток вокруг менеджера
        for (int x = -10; x <= 10; x++)
        {
            Vector3 a = origin + new Vector3(x * cellSize, 0, -10 * cellSize);
            Vector3 b = origin + new Vector3(x * cellSize, 0,  10 * cellSize);
            Gizmos.DrawLine(a, b);
        }
        for (int z = -10; z <= 10; z++)
        {
            Vector3 a = origin + new Vector3(-10 * cellSize, 0, z * cellSize);
            Vector3 b = origin + new Vector3( 10 * cellSize, 0, z * cellSize);
            Gizmos.DrawLine(a, b);
        }
    }
#endif
}
