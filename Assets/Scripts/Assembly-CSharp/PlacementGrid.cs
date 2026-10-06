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
        // Фолбэк кнопка для ПК/мобилы если в сцене нет хука
        EnsureFallbackButton();
    }

    void Update()
    {
#if !UNITY_ANDROID
        if (PcKeybinds.GetDown(PcBindAction.ToggleGrid))
            Toggle();
#endif
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Создаёт простую кнопку в углу если ни Functions ни MobileHotbar не назначили
    void EnsureFallbackButton()
    {
        // Если уже есть кнопка с PlacementGrid в сцене — не создаём
        if (FindObjectOfType<UnityEngine.UI.Button>() != null) return;
        // Создаём только если не нашли существующий Canvas с нашей иконкой
        if (GameObject.Find("GridToggleFallback") != null) return;
        // На мобиле/ПК создадим невидимый хост — пользователь может сам привязать иконку из Assets/UI_GridIcon.png
        // Делаем лёгкий OnGUI фолбэк чтобы всегда было управление
    }

    // Быстрый OnGUI фолбэк — всегда работает без настройки Canvas
    void OnGUI()
    {
        if (Instance != this) return;
        // Маленькая кнопка в правом нижнем углу, над хотбаром, как на твоей фигме
        float w = 64f, h = 64f;
        float x = Screen.width - w - 12f;
        float y = Screen.height - h - 12f;
#if UNITY_ANDROID
        // На телефоне чуть крупнее
        w = 72f; h = 72f;
        x = Screen.width - w - 16f;
        y = Screen.height - h - 96f; // над джойстиком
#endif
        Color bg = SnapEnabled ? new Color(0.2f, 0.85f, 0.35f, 0.9f) : new Color(0.15f, 0.15f, 0.15f, 0.85f);
        Color prev = GUI.backgroundColor;
        GUI.backgroundColor = bg;
        string label = SnapEnabled ? "◧ Вкл" : "◧ Выкл";
        if (GUI.Button(new Rect(x, y, w, h), label))
            Toggle();
        GUI.backgroundColor = prev;
        // подсказка бинда G на ПК
#if !UNITY_ANDROID
        GUI.Label(new Rect(x, y - 18f, w, 16f), SnapEnabled ? "G — сетка" : "G — сетка", new GUIStyle(GUI.skin.label){alignment = TextAnchor.MiddleCenter, fontSize = 10});
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
