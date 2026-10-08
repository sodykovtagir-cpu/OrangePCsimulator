using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using PC.Component;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Raycast : MonoBehaviour
{
    private class Drag
    {
        public Transform target;
        public float oldDrag;
        public float oldAngularDrag;
        public float distance;
        public RigidbodyConstraints oldConstrains;

        // Смещение от точки захвата до центра масс в локальных осях тела.
        // Нужно, чтобы по сетке снапался САМ предмет, а не только его угол.
        public Vector3 grabOffsetLocal;
    }

    [SerializeField]
    private LayerMask layer;

    [SerializeField]
    private float maxDistance = 10f;

    [SerializeField]
    private float targetSpring = 100f;

    [SerializeField]
    private float targetDamper = 5f;

    [SerializeField]
    private float targetDrag = 10f;

    [SerializeField]
    private float targetAngularDrag = 5f;

    [Header("Поворот предмета")]
    [Tooltip("Шаг и квант поворота стрелками, градусов: угол всегда кратен шагу. " +
             "Нажал — угол встал на ближайшее кратное + ещё шаг (87 -> 180, " +
             "обратно -> 90 при шаге 90).")]
    [SerializeField]
    private float rotateStep = 90f;

    [SerializeField]
    private bool showHint;

    private bool configuration;

    [SerializeField]
    private bool dragRigidbody = true;

    [SerializeField]
    [Header("UI")]
    private GameObject distanceObj;

    [SerializeField]
    private ScrollRect distanceScroll;

    [SerializeField]
    private Slider distanceSlider;

    [SerializeField]
    private Text detailText;

    private Camera cam;
    private Slot[] slots;
    private SpringJoint spring;

    // В режиме сетки пружина отключается (мгновенный снап), а здесь — чем её вернуть
    private bool gridSpringApplied;
    private float savedSpring = 100f;
    private float savedDamper = 5f;
    private Drag currentDrag;
    private PointerEventData pointer;

    public PC.Component.Display selectedMonitor;

    public bool LockRotation { get; set; }
    public bool RemoveMode { get; set; }

    public bool Configuration
    {
        get => configuration;
        set => configuration = value;
    }

    public bool AutoRotation { get; set; }

    public bool DragRigidbody
    {
        get => dragRigidbody;
        set => dragRigidbody = value;
    }

    public event Action<bool> ConfigurationStateChanged;

    private void Start()
    {
        cam = GetComponent<Camera>();
        // Гарантируем наличие сетки
        if (PlacementGrid.Instance == null)
        {
            var gridGo = new GameObject("PlacementGrid");
            gridGo.AddComponent<PlacementGrid>();
        }

        // Панель поворота предмета (стрелки) — создаём сразу, показываем при перетаскивании
        DragRotateControls.EnsureExists(this);

        // Сетка целится тем же слоем и той же камерой, что и перетаскивание
        PlacementGrid.AimLayer = layer;
        PlacementGrid.AimCamera = cam;
        var go = GameObject.Find("Touch");
        if (go == null) return;

        var et = go.GetComponent<EventTrigger>();
        if (et == null) return;

        var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        down.callback.AddListener(CalculateHit);

        var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        up.callback.AddListener(TouchExit);

        et.triggers.Add(down);
        et.triggers.Add(up);

        if (!showHint) return;
        slots = FindObjectsOfType<Slot>();
        foreach (Slot s in slots) s.EnableHint();
    }

    private void Update()
    {
#if UNITY_STANDALONE || UNITY_EDITOR || UNITY_WEBGL

    if (InputManager.GetButtonDown("Fire"))
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        pointer = null;

        ShootRaycast(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
    }

    if (InputManager.GetButtonUp("Fire"))
    {
        End();
    }

    if (currentDrag != null)
    {
        float wheel = InputManager.GetAxis("Mouse ScrollWheel");

        if (wheel != 0f)
        {
            currentDrag.distance += wheel * 3f;
            currentDrag.distance = Mathf.Clamp(currentDrag.distance, 2f, maxDistance);

            UpdateDistanceUI(currentDrag.distance);
        }

        // Поворот тащимого предмета обычными стрелками — только в режиме сетки
        if (GridMode())
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow)) RotateDragged(-1, 0);
            if (Input.GetKeyDown(KeyCode.RightArrow)) RotateDragged(1, 0);
            if (Input.GetKeyDown(KeyCode.UpArrow)) RotateDragged(0, -1);
            if (Input.GetKeyDown(KeyCode.DownArrow)) RotateDragged(0, 1);
        }
    }

#endif

        // Панель поворота (стрелки) видна только при перетаскивании в режиме
        // сетки. На ПК её нет — там поворот с клавиатуры.
        if (DragRotateControls.Instance != null)
            DragRotateControls.Instance.SetVisible(currentDrag != null && GridMode());
    }

    /// <summary>Включён ли режим сетки.</summary>
    private static bool GridMode()
    {
        return PlacementGrid.Instance != null && PlacementGrid.Instance.SnapEnabled;
    }

    private void UpdateDistanceUI(float distance)
    {
        float normalized = Conversion.Map(distance, 2f, maxDistance, 0f, 1f);

        if (distanceSlider != null)
            distanceSlider.value = normalized;

        if (distanceScroll != null)
            distanceScroll.verticalNormalizedPosition = 1f - normalized;
    }

    public void CalculateHit(BaseEventData data)
    {
        End();

        var ped = data as PointerEventData;
        if (ped != null)
        {
            pointer = ped;
            var pos = new Vector3(ped.position.x, ped.position.y, 0f);
            ShootRaycast(pos);
            return;
        }

        pointer = null;
    }

    public void TouchExit(BaseEventData data)
    {
        if (pointer == data as PointerEventData)
        {
            End();
            pointer = null;
        }
    }

    public void ShootRaycast(Vector3 pos)
    {
        var c = cam;
        if (c == null) return;

        var ray = c.ScreenPointToRay(pos);
        if (!Physics.Raycast(ray, out var hit, maxDistance, layer)) return;

        if (dragRigidbody)
        {
            var hitRb = hit.rigidbody;

            // ====== РАЗБЛОКИРУЕМ ДВЕРЬ ЕСЛИ ОНА LATCH ======
            if (hitRb != null)
            {
                var latch = hitRb.GetComponent<DoorLatch>();
                if (latch != null && latch.IsLatched)
                {
                    latch.Unlatch();
                    hitRb = latch.GetComponent<Rigidbody>(); // обновить ссылку
                }
            }
            // ===============================================

            if (hitRb && !hitRb.isKinematic && !hitRb.freezeRotation)
            {
                if (!spring)
                {
                    var go = new GameObject("Rigidbody dragger");
                    var rb = go.AddComponent<Rigidbody>();
                    rb.isKinematic = true;
                    var sj = go.AddComponent<SpringJoint>();
                    sj.spring = targetSpring;
                    sj.damper = targetDamper;
                    spring = sj;
                }

                var drag = new Drag
                {
                    target = hit.transform,
                    oldDrag = hitRb.drag,
                    oldAngularDrag = hitRb.angularDrag,
                    oldConstrains = hitRb.constraints
                };
                currentDrag = drag;

                var sTr = spring.transform;
                if (!AutoRotation)
                {
                    sTr.position = hit.point;
                    drag.distance = hit.distance;
                }
                else
                {
                    sTr.position = hitRb.worldCenterOfMass;
                    var p0 = transform.position;
                    var p1 = hitRb.worldCenterOfMass;
                    drag.distance = new Vector3(p0.x - p1.x, p0.y - p1.y, p0.z - p1.z).magnitude;
                }

                // Смещение «точка захвата -> центр масс» в осях самого тела.
                // С ним по сетке снапается корпус целиком, а не только его угол.
                drag.grabOffsetLocal = hitRb.transform.InverseTransformDirection(
                    hitRb.worldCenterOfMass - sTr.position);

                spring.connectedBody = hitRb;
                StartCoroutine("DragObject");
            }
            else if (hitRb && hitRb.isKinematic)
            {
                var hook = hit.transform ? hit.transform.GetComponentInParent<Hook>() : null;
                if (hook == null && hitRb != null)
                    hook = hitRb.GetComponent<Hook>();
                if (hook != null && hook.Hooked)
                {
                    if (!RemoveMode || !hook.RemoveModeOnly)
                    {
                        hook.ShowTip();
                    }
                }
            }
        }

        if (RemoveMode)
        {
            if (hit.transform && hit.transform.TryGetComponent<Connector>(out var conn))
                conn.Break();

            var hook = hit.transform ? hit.transform.GetComponentInParent<Hook>() : null;
            if (hook == null && hit.rigidbody != null)
                hook = hit.rigidbody.GetComponent<Hook>();
            if (hook != null && hook.Hooked && hook.RemoveModeOnly)
            {
                hook.Release();
            }
        }

        if (hit.transform && hit.transform.TryGetComponent<Item>(out var item))
        {
            var value = item.GetInfo();
            if (!string.IsNullOrEmpty(value) && detailText) detailText.text = value;
        }

        // Режим подключения имеет приоритет над обычными нажатиями.
        //
        // БАГ, который это чинит: у монитора есть дочерний объект Trigger с
        // компонентом Receiver и триггерным коллайдером — им включается
        // приближение экрана. Он активен, пока компьютер работает, и висит
        // перед экраном. Клик попадал в него, срабатывала ветка
        // IReceiverDown.Hit(), а ветка configuration до монитора не доходила
        // вовсе: она стоит в else. Игрок жал на монитор, и ничего не
        // происходило. Обходной путь — выйти из режима, взять монитор в руки и
        // отпустить: это сдвигало его, и луч начинал попадать мимо триггера.
        if (configuration)
        {
            if (HandleConfiguration(hit)) return;
        }
        else if (hit.collider && hit.collider.TryGetComponent<IReceiverDown>(out var recv))
        {
            recv.Hit();
        }

        if (!showHint) return;

        if (hit.transform && hit.transform.TryGetComponent<Hint>(out var hint))
        {
            var it = hit.transform.GetComponent<Item>();
            var arr = slots;
            if (arr == null || arr.Length < 1) return;

            for (int i = 0; i < arr.Length; i++)
            {
                var slot = arr[i];
                if (slot == null) break;
                if (hint.str == slot.target && slot.IsMatch(it.Match))
                    slot.ShowHint(true);
            }
        }
    }

    /// <summary>
    /// Шаг режима подключения: сперва выбираем монитор, затем — компьютер.
    /// </summary>
    /// <remarks>
    /// Искать компоненты нужно ВВЕРХ по иерархии, а не на том объекте, в
    /// который попал луч. И монитор, и системный блок — составные префабы:
    /// луч почти всегда попадает в дочерний объект (корпус экрана, стекло,
    /// триггер приближения, стенку корпуса), а скрипт висит на корне.
    /// Проверка тега на самом hit.transform по той же причине не работала.
    /// </remarks>
    private bool HandleConfiguration(RaycastHit hit)
    {
        var t = hit.transform;
        if (!t) return false;

        if (!selectedMonitor)
        {
            var display = t.GetComponentInParent<PC.Component.Display>();
            if (!display) return false;

            selectedMonitor = display;
            ConfigurationStateChanged?.Invoke(false);
            return true;
        }

        var mb = FindMotherboard(t);
        if (!mb) return false;

        mb.ConnectMonitor(selectedMonitor);
        ConfigurationStateChanged?.Invoke(true);
        return true;
    }

    /// <summary>
    /// Найти материнскую плату по тому, во что ткнул игрок.
    /// </summary>
    /// <remarks>
    /// Плату можно выбрать тремя способами, и все три обязаны работать:
    /// нажать на саму плату, нажать на плату внутри открытого корпуса и
    /// нажать на корпус снаружи. Последний случай — основной: закрытый
    /// системный блок вообще не даёт попасть лучом по плате, а игрок
    /// естественно жмёт на корпус.
    /// </remarks>
    private Motherboard FindMotherboard(Transform t)
    {
        var mb = t.GetComponentInParent<Motherboard>();
        if (mb) return mb;

        var pcCase = t.GetComponentInParent<PC.Component.Case>();
        if (!pcCase) return null;

        // Корпус сам платой не является: он лишь держит слот, в который она
        // вставлена. Пустой корпус подключать не к чему.
        var slot = pcCase.Motherboard;
        if (slot == null) return null;

        return slot.Hardware as Motherboard;
    }

    /// <summary>
    /// В режиме сетки положение задаёт резкий снап (TryPinAssembly), поэтому
    /// силу пружины отключаем — иначе она размазывает шаг и тянет предмет в
    /// стены. Сохранённые значения вернёт RestoreSpring.
    /// </summary>
    private void DisableSpringForGrid()
    {
        if (spring == null) return;

        if (!gridSpringApplied)
        {
            savedSpring = spring.spring;
            savedDamper = spring.damper;
            gridSpringApplied = true;
        }

        spring.spring = 0f;
        spring.damper = 0f;
    }

    /// <summary>Возвращает обычную мягкую пружину.</summary>
    private void RestoreSpring()
    {
        if (!gridSpringApplied) return;

        if (spring != null)
        {
            spring.spring = savedSpring;
            spring.damper = savedDamper;
        }

        gridSpringApplied = false;
    }

    /// <summary>
    /// Поворачивает тащимый предмет на шаг (стрелки клавиатуры или кнопки
    /// панели DragRotateControls). Вся сборка (корпус + мать + стекло, всё,
    /// что в графе джойнтов) крутится вокруг ОДНОЙ точки — иначе детали
    /// «крутятся на месте», джойнты растягиваются и при отпускании отлетают.
    /// </summary>
    public void RotateDragged(int yawDir, int pitchDir)
    {
        // Стрелки поворачивают предмет только в режиме сетки
        if (!GridMode()) return;
        if (LockRotation) return;

        var body = spring != null ? spring.connectedBody : null;
        if (body == null || currentDrag == null) return;

        float step = Mathf.Max(1f, rotateStep);
        var grid = PlacementGrid.Instance;
        var assembly = grid != null ? grid.DragAssemblyBodies : null;

        // Раскладываем ориентацию на рыскание и тангаж сами: стандартные
        // eulerAngles возле вертикали врут — из-за них предмет при нажатии
        // «вверх» падал на грань. Рыскание берём из вектора «вправо» (он
        // корректен всегда), тангаж — из «вперёд» в системе рыскания.
        var q = body.rotation;
        Vector3 right = q * Vector3.right;
        Vector3 fwd = q * Vector3.forward;

        float yaw = Mathf.Atan2(-right.z, right.x) * Mathf.Rad2Deg;
        Vector3 fLocal = Quaternion.Euler(0f, -yaw, 0f) * fwd;
        float pitch = Mathf.Atan2(-fLocal.y, fLocal.z) * Mathf.Rad2Deg;

        // Квантование: каждое нажатие ставит угол на ближайшее кратное шагу
        // и добавляет ещё шаг: было 87° — нажал вправо → 180°, обратно → 90°.
        if (yawDir != 0)
            yaw = Mathf.Round(yaw / step) * step + yawDir * step;
        if (pitchDir != 0)
            pitch = Mathf.Round(pitch / step) * step + pitchDir * step;

        // Euler(pitch, yaw, 0) = поворот вокруг вертикали на рыскание, затем
        // наклон вокруг собственной правой оси на тангаж — ровно то, что
        // делают стрелки. Последовательность «вверх»: стоит → смотрит вверх
        // → вверх ногами → смотрит вниз → стоит; без падений на грань.
        var target = Quaternion.Euler(pitch, yaw, 0f);
        var worldDelta = target * Quaternion.Inverse(q);

        // Пивот — центр габарита всей сборки: крутим вокруг него.
        Vector3 pivot = body.worldCenterOfMass;
        Bounds b;
        if (grid != null && TryAssemblyBounds(grid, body, out b))
            pivot = b.center;

        if (assembly != null && assembly.Length > 1)
        {
            for (int i = 0; i < assembly.Length; i++)
            {
                var rb = assembly[i];
                if (rb == null) continue;

                Quaternion next = worldDelta * rb.rotation;
                Vector3 pos = pivot + worldDelta * (rb.position - pivot);

                rb.MoveRotation(next);
                rb.transform.rotation = next;
                rb.MovePosition(pos);
                rb.transform.position = pos;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
        else
        {
            var next = worldDelta * body.rotation;
            body.MoveRotation(next);
            body.transform.rotation = next;
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }

    /// <summary>
    /// Габарит ВСЕЙ тащимой сборки (включая детали, которые держатся только
    /// джойнтами и не являются детьми корпуса — мать и т.п.).
    /// </summary>
    private static bool TryAssemblyBounds(PlacementGrid grid, Rigidbody body, out Bounds b)
    {
        b = default(Bounds);
        var assembly = grid.DragAssemblyBodies;
        if (assembly == null || assembly.Length == 0) return false;

        bool has = false;
        for (int i = 0; i < assembly.Length; i++)
        {
            var rb = assembly[i];
            if (rb == null) continue;

            var cs = rb.GetComponentsInChildren<Collider>(true);
            for (int j = 0; j < cs.Length; j++)
            {
                if (cs[j] == null || !cs[j].enabled) continue;

                if (!has)
                {
                    b = cs[j].bounds;
                    has = true;
                }
                else
                {
                    b.Encapsulate(cs[j].bounds);
                }
            }
        }

        return has;
    }

    /// <summary>Мировой Y низа габарита тащимой сборки.</summary>
    private float AssemblyBoundsMinY(PlacementGrid grid, Rigidbody body)
    {
        Bounds b;
        if (!TryAssemblyBounds(grid, body, out b)) return body.worldCenterOfMass.y;
        return b.min.y;
    }

    /// <summary>
    /// Резко (телепортом) смещает ВСЮ тащимую сборку на дельту — но только
    /// если в целевой позиции она ни с кем не пересекается: стены, перекрытия
    /// и другие предметы принимаются за препятствия. Сил не прикладываем —
    /// значит, ничего не ломаем, не проталкиваем и никуда не пролетаем.
    /// </summary>
    private bool TryPinAssembly(PlacementGrid grid, Rigidbody body, Vector3 delta)
    {
        if (delta.sqrMagnitude < 1e-9f) return true;

        Bounds b;
        if (!TryAssemblyBounds(grid, body, out b)) return true;

        var assembly = grid.DragAssemblyBodies;
        var self = new HashSet<Rigidbody>();
        for (int i = 0; i < assembly.Length; i++)
        {
            if (assembly[i] != null) self.Add(assembly[i]);
        }

        // Чек-бокс чуть сжат, чтобы простое касание (стоит на полу)
        // не считалось «занято».
        const float skin = 0.015f;
        Vector3 half = b.size * 0.5f - new Vector3(skin, skin, skin);
        if (half.x <= 0f || half.y <= 0f || half.z <= 0f) return true;

        var hits = Physics.OverlapBox(b.center + delta, half, Quaternion.identity, layer,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            var col = hits[i];
            if (col == null) continue;

            var rb = col.attachedRigidbody;
            if (rb != null && self.Contains(rb)) continue; // своя сборка
            if (col.transform.IsChildOf(body.transform)) continue;

            return false; // клетка занята
        }

        // Путь чист — резко встаём в клетку всей сборкой
        for (int i = 0; i < assembly.Length; i++)
        {
            var partRb = assembly[i];
            if (partRb == null) continue;

            partRb.MovePosition(partRb.position + delta);
            partRb.velocity = Vector3.zero;
            partRb.angularVelocity = Vector3.zero;
        }

        return true;
    }

    /// <summary>
    /// TryPinAssembly + «ступенька»: если клетку блокирует маленький осколок/
    /// обломок на полу — приподнимаем цель, чтобы переехать через него.
    /// Высокое (стены, другие ПК, плиты) по-прежнему блокирует.
    /// </summary>
    private bool TryPinWithClimb(PlacementGrid grid, Rigidbody body, Vector3 delta)
    {
        if (TryPinAssembly(grid, body, delta)) return true;

        float[] climbs = { 0.25f, 0.5f, grid.cellSize };
        for (int i = 0; i < climbs.Length; i++)
        {
            var up = delta;
            up.y += climbs[i];
            if (TryPinAssembly(grid, body, up)) return true;
        }

        return false;
    }


    private IEnumerator DragObject()
    {
        var j = spring;
        var rb = j ? j.connectedBody : null;
        if (rb == null) yield break;

        rb.drag = targetDrag;
        rb.angularDrag = targetAngularDrag;

        if (distanceObj)
            distanceObj.SetActive(true);

        var drag = currentDrag;
        if (drag == null)
            yield break;

        if (distanceScroll)
            distanceScroll.verticalNormalizedPosition =
                Conversion.Map(drag.distance, 2f, maxDistance, 1f, 0f);

        if (LockRotation)
        {
            rb.constraints |=
                RigidbodyConstraints.FreezeRotationX |
                RigidbodyConstraints.FreezeRotationY |
                RigidbodyConstraints.FreezeRotationZ;
        }

        var t = drag.target;
        if (!t) yield break;

        var go = t.gameObject;
        if (!go) yield break;

        int oldLayer = go.layer;

        // Защита ПК — заморозка внутренних констрейнтов
        if (PlacementGrid.Instance != null)
            PlacementGrid.Instance.OnDragStarted(t);

        while (currentDrag != null && currentDrag.target)
        {
            // В режиме сетки ориентация меняется только стрелками — автоповорот бы мешал
            if (!GridMode())
                AutoRotate();

            if (cam != null && spring)
            {
                Vector3 point;
                Ray ray;

                if (pointer != null)
                {
                    ray = cam.ScreenPointToRay(pointer.position);
                    point = ray.GetPoint(currentDrag.distance);
                }
                else
                {
                    // ПК режим - центр экрана
                    ray = cam.ScreenPointToRay(
                        new Vector3(
                            Screen.width * 0.5f,
                            Screen.height * 0.5f,
                            0f
                        )
                    );

                    point = ray.GetPoint(currentDrag.distance);
                }

                // Сетка: снапаем САМ предмет к мировой сетке с учётом стены/лифта/склона
                var grid = PlacementGrid.Instance;

                if (grid != null && grid.SnapEnabled)
                {
                    // Сначала узнаём поверхность под прицелом — по её нормали
                    // снап выбирает оси (пол снапает XZ, стена — YZ/XY).
                    // Ищем сквозь игрока и тащимый предмет: сетка ложится на
                    // пол/стену за ними, а не на них самих.
                    Vector3 normal = Vector3.up;
                    Collider surface = null;
                    Vector3 aimPoint = point;
                    bool hasSurface = grid.RaycastAimSurface(ray, maxDistance, layer, out var gridHit);

                    if (hasSurface)
                    {
                        normal = gridHit.normal;
                        surface = gridHit.collider;
                        aimPoint = gridHit.point;
                    }

                    var body = spring.connectedBody;
                    Vector3 bodySnapped;
                    Vector3 grabOffset = Vector3.zero;

                    if (body != null)
                    {
                        // Раньше снапалась точка захвата — предмет висел на ней
                        // углом и по клеткам не ходил. Теперь снапаем центр масс
                        // тела, а точку захвата просто сдвигаем вместе с ним.
                        grabOffset = body.transform.TransformDirection(currentDrag.grabOffsetLocal);
                        bodySnapped = grid.SnapPosition(point + grabOffset, normal, surface);
                    }
                    else
                    {
                        bodySnapped = grid.SnapPosition(point, normal, surface);
                    }

                    // Высота на полоподобных поверхностях: низ ПРЕДМЕТА ВМЕСТЕ
                    // С ДЕТАЛЯМИ (вся сборка) ставим на отснапанную плоскость
                    // (если она не ниже самой поверхности), чтобы предмет не
                    // уходил в пол.
                    if (body != null && hasSurface && normal.y > 0.7f)
                    {
                        float bottomOffset = body.worldCenterOfMass.y - AssemblyBoundsMinY(grid, body);
                        float bottom = grid.snapHeight
                            ? grid.SnapCoord(gridHit.point.y)
                            : gridHit.point.y;
                        if (bottom < gridHit.point.y - 0.02f) bottom = gridHit.point.y;
                        bodySnapped.y = bottom + bottomOffset;
                    }

                    point = bodySnapped - grabOffset;

                    // Визуал: сетка лежит на поверхности под прицелом,
                    // а подсвечиваем клетку, в которой окажется САМ предмет.
                    grid.ReportAim(aimPoint, normal, bodySnapped);

                    // Резко, по клеткам: клетка занимается мгновенно, но только
                    // если она свободна. Стены, перекрытия этажей и другие
                    // предметы — препятствия («боится» их). Сил не прикладываем
                    // вовсе — значит, ничего не ломаем и не проталкиваем.
                    // По вертикали — не больше клетки за кадр: «вверх/вниз по
                    // сетке» ступеньками, без странного слома к земле.
                    if (body != null)
                    {
                        Vector3 comOffset = body.worldCenterOfMass - body.position;
                        Vector3 delta = (bodySnapped - comOffset) - body.position;

                        // Если предмет провален в пол (например, после
                        // переворота) — поднимаем за один раз, иначе он так и
                        // останется в полу: ступенчатый лимит не даёт выбраться.
                        bool sunk = hasSurface && normal.y > 0.7f &&
                                    AssemblyBoundsMinY(grid, body) < gridHit.point.y - 0.02f;
                        if (!sunk)
                            delta.y = Mathf.Clamp(delta.y, -grid.cellSize, grid.cellSize);

                        if (!TryPinWithClimb(grid, body, delta) && hasSurface && normal.y > 0.7f)
                        {
                            // С отснапанной высотой клетка занята — пробуем
                            // встать прямо на поверхность.
                            float bottomOffset = body.worldCenterOfMass.y - AssemblyBoundsMinY(grid, body);
                            bodySnapped.y = gridHit.point.y + bottomOffset;
                            point = bodySnapped - grabOffset;
                            delta = (bodySnapped - comOffset) - body.position;
                            if (!sunk)
                                delta.y = Mathf.Clamp(delta.y, -grid.cellSize, grid.cellSize);
                            TryPinWithClimb(grid, body, delta);
                        }
                    }

                    DisableSpringForGrid();
                }
                else
                {
                    RestoreSpring();
                }

                spring.transform.position = point;

                var curGo = currentDrag.target
                    ? currentDrag.target.gameObject
                    : null;

                if (!curGo)
                    yield break;

                int newLayer = curGo.layer;

                if (newLayer != oldLayer)
                {
                    if (newLayer == 0)
                    {
                        oldLayer = newLayer;
                    }
                    else
                    {
                        End();
                        yield break;
                    }
                }
            }

            yield return null;
        }
    }

    public void AutoRotate()
    {
        if (!AutoRotation) return;

        var j = spring;
        var rb = j?.connectedBody;
        if (rb == null || rb.isKinematic) return;

        var t = transform;
        var target = currentDrag?.target;
        if (t == null || target == null) return;

        var tp = target.position;
        var dir = new Vector3(t.position.x - tp.x, 0f, t.position.z - tp.z);
        var desired = Quaternion.LookRotation(dir);

        var a = target.rotation;
        var angle = Quaternion.Angle(a, desired);
        if (angle > 0f)
        {
            var step = 1f / angle;
            if (step > 1f) step = 1f;
            desired = Quaternion.SlerpUnclamped(a, desired, step);
        }

        rb.MoveRotation(desired);
    }

    public void OnDistanceChanged(UnityEngine.Vector2 value)
    {
        var s = distanceSlider;
        if (s == null) return;

        s.value = 1f - value.y;

        var drag = currentDrag;
        if (drag == null) return;

        var v = s.value;
        if (v > 1f) v = 1f;
        if (v < 0f) v = 0f;

        drag.distance = (maxDistance - 2f) * v + 2f;
    }

    public void End()
    {
        // Обычную мягкую пружину вернём до того, как отпустим тело
        RestoreSpring();

        if (PlacementGrid.Instance != null)
            PlacementGrid.Instance.OnDragEnded();

        if (currentDrag != null)
        {
            StopCoroutine("DragObject");

            if (spring && spring.connectedBody)
            {
                var rb = spring.connectedBody;
                var d = currentDrag;
                rb.constraints = d.oldConstrains;
                rb.drag = d.oldDrag;
                rb.angularDrag = d.oldAngularDrag;
                // ====== ЗАЩЁЛКНУТЬ ДВЕРЬ ЕСЛИ ЭТО ОНА ======
                var latch = rb.GetComponent<DoorLatch>();
                if (latch != null)
                {
                    latch.Latch();
                }
                // ============================================

                spring.connectedBody = null;
            }

            if (distanceObj) distanceObj.SetActive(false);
            currentDrag = null;

            if (showHint && slots != null)
            {
                foreach (var s in slots)
                {
                    if (s == null) continue;
                    s.ShowHint(false);
                }
            }
        }

        if (detailText) detailText.text = "\n";
    }
}