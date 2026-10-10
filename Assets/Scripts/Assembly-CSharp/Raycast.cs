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

        // Сетка уже вела этот перенос (пружина выключена, предмет держит снап).
        public bool gridTouched;
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

    // Стрелки в режиме сетки: режим (перемещение/поворот) и накопленный
    // горизонтальный шаг, который применит следующий кадр драга.
    private bool gridArrowMoveMode = true;
    private Vector3 pendingGridMove;
    private bool gridMoveInit;

#if UNITY_STANDALONE || UNITY_EDITOR || UNITY_WEBGL
    // Подпись режима стрелок в левом нижнем углу (только ПК).
    private GameObject modeHint;
    private Text modeHintName;
    private Text modeHintTip;
    private float nextModeHintTry;
#endif

    // Двухступенчатый хват: первое нажатие — выбор с обводкой,
    // второе — перенос. Лёгкие предметы не сносит пружиной от
    // случайного касания.
    private Rigidbody selectedBody;
    private List<GameObject> outlineShells;
    private Material outlineMat;
    // id тел стопки, которые сейчас обведены (для перестройки при смене состава)
    private int[] shellStackIds;

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

    // Повторная попытка собрать панель стрелок, если в Start не вышло
    private float nextArrowPanelTry;

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

    // R — переключить режим стрелок «перемещение / поворот». Работает, пока
    // включена сетка: и когда предмет только выбран, и когда он уже в руках.
    // Раньше проверка сидела внутри переноса, и до захвата R не работала.
    // Стоит первой, чтобы ранний выход из блока Fire её не пропускал.
    if (GridMode() && !IsTypingInInputField() && Input.GetKeyDown(KeyCode.R))
        ToggleArrowMode();

    // Стрелки в режиме сетки — и для зажатого предмета, и для выбранного без
    // зажатия: ArrowInput сам решает, что двигать. Ctrl — уменьшенный шаг.
    // Стоят до ЛКМ: ранний выход из блока Fire их не должен пропускать.
    if (GridMode() && !IsTypingInInputField())
    {
        bool fine = Input.GetKey(KeyCode.LeftControl) ||
                    Input.GetKey(KeyCode.RightControl);

        HoldArrow(KeyCode.LeftArrow, -1, 0, fine, 0);
        HoldArrow(KeyCode.RightArrow, 1, 0, fine, 1);
        HoldArrow(KeyCode.UpArrow, 0, 1, fine, 2);
        HoldArrow(KeyCode.DownArrow, 0, -1, fine, 3);
    }

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
    }

#endif

#if UNITY_STANDALONE || UNITY_EDITOR || UNITY_WEBGL
        RefreshModeHint();
#endif

        // Панель поворота (стрелки) видна, пока предмет выбран или тащат его в
        // режиме сетки. На ПК её нет — там стрелки с клавиатуры.
        if (DragRotateControls.Instance != null)
            DragRotateControls.Instance.SetVisible(GridMode() && (currentDrag != null || selectedBody != null));

        // Панель могла не собраться в Start (HUD ещё не был готов) — пробуем
        // ещё раз, но не чаще раза в полсекунды. Раньше панель строилась один раз
        // и при неудаче пропадала навсегда. На ПК EnsureExists сразу выходит.
        if (Time.unscaledTime >= nextArrowPanelTry)
        {
            nextArrowPanelTry = Time.unscaledTime + 0.5f;
            DragRotateControls.EnsureExists(this);
        }

        // Обводка выбора — тоже только в режиме сетки
        if (!GridMode() && selectedBody != null) ClearSelection();

        // Выбранный предмет уничтожили (удаление/смена сцены): снимаем выбор,
        // иначе замок кинематики останется висеть вплоть до следующего клика.
        if (selectedBody == null && shellStackIds != null) ClearSelection();
    }

    private void LateUpdate()
    {
        // Обводка всей стопки: если её состав изменился (что-то положили
        // сверху / сняли) — перестроить. Дёшево: сравнение id раз в кадр.
        if (selectedBody == null || !GridMode()) return;
        SyncSelectionShells();
    }

    /// <summary>Включён ли режим сетки.</summary>
    private static bool GridMode()
    {
        return PlacementGrid.Instance != null && PlacementGrid.Instance.SnapEnabled;
    }

#if UNITY_STANDALONE || UNITY_EDITOR || UNITY_WEBGL
    /// <summary>
    /// Подпись режима стрелок в левом нижнем углу экрана: «Перемещение» или
    /// «Поворот» и подсказка про R. Видна, пока включена сетка. Только ПК: на
    /// телефоне в этом углу джойстик, а режим показывает кнопка панели поворота.
    /// </summary>
    private void CreateModeHint()
    {
        if (modeHint != null) return; // Unity-null: после смены сцены создадим заново

        var canvas = DragRotateControls.FindHudCanvas();
        if (canvas == null) return;

        modeHint = new GameObject("GridModeHint", typeof(RectTransform));
        modeHint.transform.SetParent(canvas.transform, false);
        modeHint.hideFlags = HideFlags.DontSave;

        var rect = (RectTransform)modeHint.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(16f, 16f);
        rect.sizeDelta = new Vector2(250f, 56f);

        var bg = modeHint.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.45f);
        bg.raycastTarget = false; // подпись не должна перехватывать клики

        // Шрифт — тот же, что у текста информации о предмете: в нём есть буквы
        // языков, которые выбрал игрок. Если его нет — встроенный.
        var font = detailText != null && detailText.font != null
            ? detailText.font
            : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        modeHintName = MakeModeHintText(modeHint.transform, font, 20, 26f, -4f, Color.white);
        modeHintTip = MakeModeHintText(modeHint.transform, font, 13, 6f, -30f,
            new Color(1f, 1f, 1f, 0.75f));

        modeHint.SetActive(false);
    }

    private static Text MakeModeHintText(Transform parent, Font font, int size,
        float bottomOffset, float topOffset, Color color)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(12f, bottomOffset);
        rect.offsetMax = new Vector2(-12f, topOffset);

        var text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
        outline.effectDistance = new Vector2(1f, -1f);

        return text;
    }

    /// <summary>
    /// Обновляет подпись: видна при включённой сетке, текст — по языку. Создаём её
    /// при первом включении сетки, а не в Start: хотбар ПК включает ControlRig в
    /// своём Start, и порядок Start между объектами не гарантирован. Если HUD ещё
    /// не найден, повторяем не чаще раза в полсекунды.
    /// </summary>
    private void RefreshModeHint()
    {
        bool gridOn = GridMode();

        if (modeHint == null)
        {
            if (!gridOn || Time.unscaledTime < nextModeHintTry) return;

            nextModeHintTry = Time.unscaledTime + 0.5f;
            CreateModeHint();
            if (modeHint == null) return;
        }

        if (modeHint.activeSelf != gridOn) modeHint.SetActive(gridOn);
        if (!gridOn) return;

        // Текст из таблицы переводов (Translate.txt): при смене языка подпись тоже меняется
        modeHintName.text = Localization.GetText(
            gridArrowMoveMode ? "Grid mode: move" : "Grid mode: rotate");
        // Блокировка поворота (клавиша 2): в режиме поворота стрелки не крутят.
        // Показываем её вместо подсказки про R, чтобы было видно, почему не крутится.
        modeHintTip.text = !gridArrowMoveMode && LockRotation
            ? Localization.GetText("Lock Rotation")
            : Localization.GetText("Grid mode: R to switch");
    }

    /// <summary>
    /// Печатают ли сейчас в текстовом поле (блокнот, редактор кода и т.п.).
    /// Тогда R и стрелки — это ввод текста, а не управление сеткой.
    /// </summary>
    private static bool IsTypingInInputField()
    {
        var es = EventSystem.current;
        if (es == null || es.currentSelectedGameObject == null) return false;

        var field = es.currentSelectedGameObject.GetComponent<InputField>();
        return field != null && field.isFocused;
    }

    // Зажатая стрелка повторяется, как клавиша в тексте: первый шаг — по нажатию,
    // потом пауза и повтор. Поворот повторяется медленнее, чем перемещение.
    private const float ArrowRepeatDelay = 0.3f;
    private const float MoveRepeatInterval = 0.14f;
    private const float RotateRepeatInterval = 0.35f;
    private readonly float[] arrowNextAt = new float[4];

    private void HoldArrow(KeyCode key, int xDir, int yDirUp, bool fine, int slot)
    {
        float now = Time.unscaledTime;

        if (Input.GetKeyDown(key))
        {
            ArrowInput(xDir, yDirUp, fine);
            arrowNextAt[slot] = now + ArrowRepeatDelay;
        }
        else if (Input.GetKey(key) && now >= arrowNextAt[slot])
        {
            ArrowInput(xDir, yDirUp, fine);
            arrowNextAt[slot] = now + (ArrowMoveMode ? MoveRepeatInterval : RotateRepeatInterval);
        }
    }
#endif

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
        if (!Physics.Raycast(ray, out var hit, maxDistance, layer))
        {
            // Нажали в пустоту — снимаем выбор и обводку
            if (currentDrag == null) ClearSelection();
            return;
        }

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

            // Нажали на то, что нельзя подвинуть (стена, пол, кинематика,
            // закреплённое тело) — выбор снимаем, как и при клике в пустоту.
            // Во время переноса кнопка зажата, сюда не попадаем.
            // ВЫБРАННЫЙ предмет в режиме сетки тоже «нельзя подвинуть» с точки
            // зрения физики (он заморожен кинематикой), но второй по нему клик
            // — это захват, поэтому считаем его движимым.
            bool movable = hitRb &&
                (hitRb == selectedBody ||
                 (!hitRb.isKinematic && !hitRb.freezeRotation));
            if (!movable && GridMode() && currentDrag == null) ClearSelection();

            if (movable)
            {
                // Двухступенчатый хват (только в режиме сетки): первое
                // нажатие только выбирает предмет (обводка), второе по
                // выбранному — тащит. Лёгкие предметы не улетают от
                // случайного касания. Без сетки — хват сразу, как раньше.
                if (GridMode() && !RemoveMode && currentDrag == null &&
                    selectedBody != hitRb)
                {
                    SelectBody(hitRb);
                    return;
                }

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
    /// Выбирает предмет БЕЗ захвата: рисует обводку (inverted hull) по нему и
    /// всем частям его стопки и замораживает сборку (кинематика — физика её не
    /// трогает). Следующее нажатие по выбранному — настоящий перенос.
    /// </summary>
    private void SelectBody(Rigidbody rb)
    {
        ClearSelection();
        selectedBody = rb;

        // Физический замок выбора: пока предмет выбран в режиме сетки, его
        // сборка кинематическая — физика не может ни сдвинуть, ни повернуть
        // (гравитация, удары, «пропавший» при быстром повороте предмет).
        // Сборка и обводка стопки живут до снятия выбора.
        var grid = PlacementGrid.Instance;
        if (grid != null) grid.OnDragStarted(rb.transform);

        RebuildSelectionShells();
    }

    /// <summary>
    /// Перерисовывает обводку: выбранный предмет И все части его стопки
    /// (сборка по джойнтам + свободные предметы сверху) — видно, что именно
    /// будет ехать вместе с ним. Состав стопки берём из сетки — поля
    /// DragAssemblyBodies.
    /// </summary>
    private void RebuildSelectionShells()
    {
        DestroyShellObjects();
        shellStackIds = null;

        if (selectedBody == null) return;

        var grid0 = PlacementGrid.Instance;
        var asm0 = grid0 != null ? grid0.DragAssemblyBodies : null;
        shellStackIds = asm0 != null && asm0.Length > 0
            ? ToIds(asm0)
            : new[] { selectedBody.GetInstanceID() };

        if (!EnsureOutlineMat()) return;

        outlineShells = new List<GameObject>();
        var covered = new List<Transform>();

        // Выбранный — всегда первый: его обводим в любом случае.
        ShellBody(selectedBody, covered);

        if (asm0 != null)
        {
            for (int i = 0; i < asm0.Length; i++)
            {
                var part = asm0[i];
                if (part != null && part != selectedBody) ShellBody(part, covered);
            }
        }
    }

    /// <summary>Ставит/проверяет материал обводки. false — шейдера нет в билде.</summary>
    private bool EnsureOutlineMat()
    {
        if (outlineMat != null) return true;
        var sh = Shader.Find("Orange/Outline");
        if (sh == null) return false;
        outlineMat = new Material(sh);
        return true;
    }

    private static int[] ToIds(Rigidbody[] bodies)
    {
        var ids = new List<int>(bodies.Length);
        for (int i = 0; i < bodies.Length; i++)
            if (bodies[i] != null) ids.Add(bodies[i].GetInstanceID());
        ids.Sort();
        return ids.ToArray();
    }

    /// <summary>
    /// Обводит все меши тела. Уже накрытые предками тела пропускаем — иначе
    /// меш ребёнка обводится дважды (и от родителя, и от него самого).
    /// </summary>
    private void ShellBody(Rigidbody rb, List<Transform> covered)
    {
        if (rb == null) return;

        var t = rb.transform;
        for (int i = 0; i < covered.Count; i++)
        {
            var root = covered[i];
            if (root != null && (t == root || t.IsChildOf(root))) return;
        }
        covered.Add(t);

        var filters = rb.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            var f = filters[i];
            if (f == null || f.sharedMesh == null) continue;
            AddShell(f.transform, f.sharedMesh, null);
        }

        var skins = rb.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < skins.Length; i++)
        {
            var s = skins[i];
            if (s == null || s.sharedMesh == null) continue;
            AddShell(s.transform, s.sharedMesh, s);
        }
    }

    /// <summary>
    /// Если состав стопки изменился (на предмет что-то положили/сняли) —
    /// обводку перестраиваем. Дёшево: сравнение id раз в кадр.
    /// </summary>
    private void SyncSelectionShells()
    {
        if (selectedBody == null) return;

        var grid = PlacementGrid.Instance;
        var assembly = grid != null ? grid.DragAssemblyBodies : null;

        int[] ids;
        if (assembly != null && assembly.Length > 0) ids = ToIds(assembly);
        else ids = new[] { selectedBody.GetInstanceID() };

        var old = shellStackIds;
        if (old != null && old.Length == ids.Length)
        {
            bool same = true;
            for (int i = 0; i < old.Length; i++)
                if (old[i] != ids[i]) { same = false; break; }
            if (same) return;
        }

        RebuildSelectionShells();
    }

    private void DestroyShellObjects()
    {
        if (outlineShells == null) return;
        for (int i = 0; i < outlineShells.Count; i++)
        {
            if (outlineShells[i] != null) Destroy(outlineShells[i]);
        }
        outlineShells = null;
    }

    private void AddShell(Transform parent, Mesh mesh, SkinnedMeshRenderer skin)
    {
        var go = new GameObject("OutlineShell");
        go.hideFlags = HideFlags.DontSave;

        if (skin != null)
        {
            go.transform.SetParent(skin.transform.parent, false);
            go.transform.localPosition = skin.transform.localPosition;
            go.transform.localRotation = skin.transform.localRotation;
            go.transform.localScale = skin.transform.localScale;

            var sr = go.AddComponent<SkinnedMeshRenderer>();
            sr.sharedMesh = mesh;
            sr.rootBone = skin.rootBone;
            sr.bones = skin.bones;
            sr.sharedMaterial = outlineMat;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sr.receiveShadows = false;
        }
        else
        {
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = outlineMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        outlineShells.Add(go);
    }

    /// <summary>Снимает выбор: убирает обводку (всей стопки) и отпускает физический замок.</summary>
    public void ClearSelection()
    {
        DestroyShellObjects();
        shellStackIds = null;
        selectedBody = null;

        // Замок выбора держал PlacementGrid (OnDragStarted в SelectBody).
        // Во время переноса (currentDrag) не трогаем — там свой жизненный
        // цикл, замок отпустит End().
        if (currentDrag == null)
        {
            var grid = PlacementGrid.Instance;
            if (grid != null) grid.OnDragEnded();
        }
    }

    /// <summary>Стрелки сейчас перемещают (true) или поворачивают (false).</summary>
    public bool ArrowMoveMode { get { return gridArrowMoveMode; } }

    /// <summary>R / кнопка панели: переключить режим стрелок.</summary>
    public bool ToggleArrowMode()
    {
        gridArrowMoveMode = !gridArrowMoveMode;
        return gridArrowMoveMode;
    }

    /// <summary>
    /// Стрелки (клавиатура или панель): в режиме перемещения — шаг по клеткам
    /// в МИРОВЫХ осях (вправо = +X, «вперёд» = +Z), не зависимо от поворота
    /// камеры, Ctrl/мелкие стрелки — шаг в пятую часть клетки; в режиме
    /// поворота — поворот (вверх = кувырок назад). Движение резко, через те же
    /// гейты: стена не пустит, к соседу можно прислонить впритык.
    /// </summary>
    public void ArrowInput(int xDir, int yDirUp, bool fine)
    {
        if (!GridMode()) return;

        // Предмет зажат — стрелки двигают/крутят его (перенос). Выбран, но не
        // зажат — шаг или поворот сразу (см. NudgeSelected).
        if (currentDrag == null)
        {
            if (selectedBody != null) NudgeSelected(xDir, yDirUp, fine);
            return;
        }

        if (!gridArrowMoveMode)
        {
            RotateDragged(xDir, -yDirUp, fine);
            return;
        }

        var grid = PlacementGrid.Instance;
        if (grid == null) return;

        // Мировые оси, а не камеры: влево/вправо — ±X, «вперёд»/«назад» —
        // ±Z. Раньше шаг считался от направления камеры, и один и тот же
        // клик стрелкой двигал предмет в разные стороны при повороте взгляда.
        float step = fine ? grid.cellSize * 0.2f : grid.cellSize;
        pendingGridMove +=
            (Vector3.right * (float)xDir + Vector3.forward * (float)yDirUp) * step;
    }

    /// <summary>
    /// Стрелка по ВЫБРАННОМУ, но не зажатому предмету: один шаг по клетке или
    /// один поворот — сразу, в этом же кадре. Замок выбора (кинематика) уже
    /// стоит с момента выделения — на время шага лишь подстраховываемся, что
    /// сборка собрана, и НЕ отпускаем: между нажатиями предмет не падает,
    /// не качается и не поворачивается от физики. Смещение — по мировым осям,
    /// через те же гейты (стена, соседний предмет, подъём).
    /// </summary>
    private void NudgeSelected(int xDir, int yDirUp, bool fine)
    {
        var grid = PlacementGrid.Instance;
        var body = selectedBody;
        if (grid == null || body == null) return;

        // Подстраховка: замок должен быть (выбор уже заморозил сборку)
        if (grid.DragAssemblyBodies == null || grid.DragTarget != body.transform)
            grid.OnDragStarted(body.transform);

        if (!gridArrowMoveMode)
        {
            RotateDragged(xDir, -yDirUp, fine);
            return;
        }

        // Мировые оси (как и при переносе): влево/вправо — ±X, «вперёд» — +Z
        float step = fine ? grid.cellSize * 0.2f : grid.cellSize;
        Vector3 delta =
            (Vector3.right * (float)xDir + Vector3.forward * (float)yDirUp) * step;

        // Просевший в стол/стену предмет сначала выталкиваем: иначе габарит
        // считается от утопленной точки, и PhysX выстрелит его при отпускании.
        PlacementGrid.UnstickAssembly(grid.DragAssemblyBodies, grid.surfaceMask);

        // Обычный шаг сперва снапает центр масс к клетке: после физики предмет
        // мог чуть съехать. Тонкий шаг — от текущего места, без снапа.
        if (!fine)
        {
            Vector3 com = body.worldCenterOfMass;
            Vector3 snapped = grid.SnapPosition(com, Vector3.up, null);
            delta += new Vector3(snapped.x - com.x, 0f, snapped.z - com.z);
        }

        delta.y = 0f;
        if (delta.sqrMagnitude < 1e-10f) return;

        TryPinWithClimb(grid, body, delta, true);
    }

    /// <summary>
    /// Поворачивает тащимый предмет на шаг (стрелки клавиатуры или кнопки
    /// панели DragRotateControls). Вся сборка (корпус + мать + стекло, всё,
    /// что в графе джойнтов) крутится вокруг ОДНОЙ точки — иначе детали
    /// «крутятся на месте», джойнты растягиваются и при отпускании отлетают.
    /// </summary>
    public void RotateDragged(int yawDir, int pitchDir, bool fine = false)
    {
        // Стрелки поворачивают предмет только в режиме сетки
        if (!GridMode()) return;
        if (LockRotation) return;

        // Тащимый предмет; если его не тащат — выбранный (см. NudgeSelected)
        Rigidbody body = currentDrag != null
            ? (spring != null ? spring.connectedBody : null)
            : selectedBody;
        if (body == null) return;

        // Замок выбора: при выделении сборка уже собрана и заморожена, здесь
        // лишь подстраховка (например, после смены сцены/сбоя состояния).
        if (currentDrag == null)
        {
            var g0 = PlacementGrid.Instance;
            if (g0 != null &&
                (g0.DragAssemblyBodies == null || g0.DragTarget != body.transform))
                g0.OnDragStarted(body.transform);
        }

        // Ctrl/мелкие стрелки — уменьшенный шаг поворота
        float step = Mathf.Max(1f, fine ? rotateStep / 6f : rotateStep);
        var grid = PlacementGrid.Instance;
        var assembly = grid != null ? grid.DragAssemblyBodies : null;

        // Поворот — тоже телепорт: сперва выталкиваем из просадки, иначе
        // предмет уйдёт в пол/стол вместе с «застрявшей» частью.
        if (grid != null) PlacementGrid.UnstickAssembly(assembly, grid.surfaceMask);

        // Ориентация — кратная 90° по осям коробки. Раньше поворот раскладывали на
        // рыскание и тангаж: у корпуса, лежащего на боку («дыркой вверх»), правая
        // ось вертикальна, рыскание вырождалось, крен терялся, и поворот шёл на 120°.
        // Теперь база — ближайшая коробочная ориентация, а кувырок — на 90° вокруг
        // горизонтальной оси коробки (см. TumbleAxis). Для стоящего корпуса итог
        // тот же, что и раньше.
        var q = body.rotation;
        Quaternion qBase = fine ? q : SnapToBoxOrientation(q);
        Quaternion target;

        if (yawDir != 0)
        {
            // Рыскание — вокруг мировой вертикали
            target = Quaternion.AngleAxis(yawDir * step, Vector3.up) * qBase;
        }
        else if (pitchDir != 0)
        {
            Vector3 axis = TumbleAxis(qBase, CameraRightHorizontal());
            // «вверх» (pitchDir < 0) — поднять переднюю грань, кувырок назад
            target = Quaternion.AngleAxis(-pitchDir * step * TumbleSign(qBase, axis), axis) * qBase;
        }
        else
        {
            return;
        }

        var worldDelta = target * Quaternion.Inverse(q);

        // Пивот — центр физического габарита всей сборки. Триггеры в него не входят:
        // зона приближения у монитора раздувала габарит, и крышка «уезжала» вокруг
        // далёкого пивота.
        Vector3 pivot = body.worldCenterOfMass;
        Bounds cur = default(Bounds);
        bool hasBounds = grid != null && TryAssemblyBounds(grid, body, out cur);
        if (hasBounds) pivot = cur.center;

        var self = new HashSet<Rigidbody>();
        if (assembly != null)
        {
            for (int i = 0; i < assembly.Length; i++)
                if (assembly[i] != null) self.Add(assembly[i]);
        }

        // Предсказываем габарит ПОСЛЕ поворота по точным углам коробочных коллайдеров
        // (AABB-углы повёрнутого тела завышали габарит). Низ после поворота не должен
        // опуститься ниже уровня, на котором предмет стоял до поворота.
        float minBefore = hasBounds ? cur.min.y : body.worldCenterOfMass.y;
        var corners = new List<Vector3>();
        if (assembly != null)
        {
            for (int i = 0; i < assembly.Length; i++)
            {
                var rb = assembly[i];
                if (rb == null) continue;

                var cs = rb.GetComponentsInChildren<Collider>(true);
                for (int j = 0; j < cs.Length; j++)
                {
                    if (PlacementGrid.IsSolidCollider(cs[j])) AddColliderCorners(cs[j], corners);
                }
            }
        }

        Bounds pred = default(Bounds);
        bool hasPred = corners.Count > 0;
        if (hasPred)
        {
            Vector3 mn = Vector3.positiveInfinity;
            Vector3 mx = Vector3.negativeInfinity;
            for (int i = 0; i < corners.Count; i++)
            {
                Vector3 p = pivot + worldDelta * (corners[i] - pivot);
                mn = Vector3.Min(mn, p);
                mx = Vector3.Max(mx, p);
            }
            pred = new Bounds((mn + mx) * 0.5f, mx - mn);
        }

        float predLift = hasPred ? Mathf.Max(0f, minBefore - pred.min.y) : 0f;
        if (hasPred) pred.center += new Vector3(0f, predLift, 0f);

        // Гейт поворота: если предсказанный (приподнятый) габарит кого-то
        // задевает — поворот отменяется целиком.
        if (hasPred)
        {
            const float skin = 0.005f;
            Vector3 half = pred.size * 0.5f - new Vector3(skin, skin, skin);
            if (half.x > 0f && half.y > 0f && half.z > 0f)
            {
                var blocked = Physics.OverlapBox(pred.center, half,
                    Quaternion.identity, grid.surfaceMask, QueryTriggerInteraction.Ignore);

                for (int i = 0; i < blocked.Length; i++)
                {
                    var col = blocked[i];
                    if (col == null) continue;

                    var crb = col.attachedRigidbody;
                    if (crb != null && self.Contains(crb)) continue;
                    if (grid.IsAimBlocker(col)) continue;

                    // после поворота стоим НА нём (пол/стол) — не блок
                    if (col.bounds.max.y <= pred.min.y + GroundEpsilon) continue;

                    return; // не крутим — заденем
                }
            }
        }

        // Поворот вокруг пивота — прямой записью трансформов. MoveRotation/MovePosition
        // оставляли цель до следующего шага физики: если тело в этот момент становилось
        // динамическим (отпустили кнопку), оно разгонялось и «взлетало».
        var parts = assembly != null && assembly.Length > 0 ? assembly : new[] { body };
        for (int i = 0; i < parts.Length; i++)
        {
            var rb = parts[i];
            if (rb == null) continue;

            Quaternion next = worldDelta * rb.transform.rotation;
            Vector3 pos = pivot + worldDelta * (rb.transform.position - pivot);
            rb.transform.SetPositionAndRotation(pos, next);
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        Physics.SyncTransforms();

        // Подъём — по ФАКТИЧЕСКОМУ низу после поворота: низ остаётся на прежней высоте.
        // Предсказанный подъём каждый раз немного завышал, и за несколько быстрых
        // поворотов предмет «подкидывало» всё выше.
        Bounds after;
        if (hasBounds && grid != null && TryAssemblyBounds(grid, body, out after))
        {
            float lift = Mathf.Max(0f, minBefore - after.min.y);
            if (lift > 0f)
            {
                for (int i = 0; i < parts.Length; i++)
                {
                    var rb = parts[i];
                    if (rb != null) rb.transform.position += new Vector3(0f, lift, 0f);
                }
                Physics.SyncTransforms();
            }
        }
    }

    // 24 коробочные ориентации (кратные 90° по всем осям). 64 комбинации углов Эйлера
    // дают все 24; повторы не мешают.
    private static Quaternion[] boxOrientations;

    /// <summary>Ближайшая коробочная ориентация к q: поворот на 90° оставляет коробку коробкой.</summary>
    private static Quaternion SnapToBoxOrientation(Quaternion q)
    {
        if (boxOrientations == null)
        {
            var list = new List<Quaternion>(64);
            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                    for (int z = 0; z < 4; z++)
                        list.Add(Quaternion.Euler(90f * x, 90f * y, 90f * z));
            boxOrientations = list.ToArray();
        }

        Quaternion best = boxOrientations[0];
        float bestDot = -1f;
        for (int i = 0; i < boxOrientations.Length; i++)
        {
            float d = Mathf.Abs(Quaternion.Dot(q, boxOrientations[i]));
            if (d > bestDot)
            {
                bestDot = d;
                best = boxOrientations[i];
            }
        }
        return best;
    }

    /// <summary>Горизонтальное «вправо» камеры — для выбора оси кувырка.</summary>
    private static Vector3 CameraRightHorizontal()
    {
        var cam = Camera.main;
        Vector3 r = cam != null ? cam.transform.right : Vector3.right;
        r.y = 0f;
        return r.sqrMagnitude < 1e-6f ? Vector3.right : r.normalized;
    }

    /// <summary>
    /// Ось кувырка для стрелок вверх/вниз — горизонтальная ось коробки. Обычно это
    /// правая ось (как и раньше). Если корпус лежит на боку, правая ось вертикальна:
    /// тогда берём переднюю или верхнюю ось — ту, что ближе к «вправо» от камеры.
    /// </summary>
    private static Vector3 TumbleAxis(Quaternion qb, Vector3 camRightH)
    {
        Vector3 right = qb * Vector3.right;
        if (Mathf.Abs(right.y) < 0.5f) return right;

        Vector3 front = qb * Vector3.forward;
        Vector3 top = qb * Vector3.up;
        return Mathf.Abs(Vector3.Dot(front, camRightH)) >= Mathf.Abs(Vector3.Dot(top, camRightH))
            ? front
            : top;
    }

    /// <summary>
    /// Направление кувырка: +1, если поворот на +90° вокруг оси поднимает переднюю
    /// (или верхнюю) грань. Так «вверх» поднимает переднюю грань, как и раньше.
    /// </summary>
    private static float TumbleSign(Quaternion qb, Vector3 axis)
    {
        Vector3 front = qb * Vector3.forward;
        Vector3 top = qb * Vector3.up;
        Vector3 mf = Vector3.Cross(axis, front);
        Vector3 mt = Vector3.Cross(axis, top);
        Vector3 m = Mathf.Abs(mf.y) >= Mathf.Abs(mt.y) ? mf : mt;
        return m.y >= 0f ? 1f : -1f;
    }

    /// <summary>
    /// Углы коллайдера в мире — для предсказания габарита после поворота. У коробки
    /// берём её собственные углы (OBB), а не углы AABB: AABB завышал габарит.
    /// </summary>
    private static void AddColliderCorners(Collider c, List<Vector3> into)
    {
        var box = c as BoxCollider;
        if (box != null)
        {
            Vector3 e = box.size * 0.5f;
            for (int k = 0; k < 8; k++)
            {
                Vector3 local = box.center + new Vector3(
                    (k & 1) != 0 ? e.x : -e.x,
                    (k & 2) != 0 ? e.y : -e.y,
                    (k & 4) != 0 ? e.z : -e.z);
                into.Add(box.transform.TransformPoint(local));
            }
            return;
        }

        Bounds wb = c.bounds;
        for (int k = 0; k < 8; k++)
        {
            into.Add(new Vector3(
                (k & 1) != 0 ? wb.max.x : wb.min.x,
                (k & 2) != 0 ? wb.max.y : wb.min.y,
                (k & 4) != 0 ? wb.max.z : wb.min.z));
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

        // Только физические коллайдеры: триггеры (зоны приближения, подсказки) и
        // выключенные части габарит не раздувают — иначе соседи «не двигаются».
        bool has = false;
        for (int i = 0; i < assembly.Length; i++)
        {
            Bounds part;
            if (!PlacementGrid.TrySolidBounds(assembly[i], out part)) continue;

            if (!has)
            {
                b = part;
                has = true;
            }
            else
            {
                b.Encapsulate(part);
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

    // Допуск, в пределах которого коллайдер считается «полом под нами», а не
    // препятствием. Раньше здесь было 0.06 м: предмет на 6 см влезал внутрь
    // стола/тумбы, а потом физика выбрасывала его. Ступеньку теперь гасит
    // посадка на опору (SettleAssembly), а просадку — выталкивание
    // (PlacementGrid.UnstickAssembly), поэтому хватает контактного допуска PhysX.
    private const float GroundEpsilon = 0.015f;

    /// <summary>
    /// Гасит подъём после шага «через препятствие»: предмет остаётся висеть
    /// на высоте ступеньки, а в режиме сетки он кинематический и сам не упадёт
    /// — при зажатой стрелке он так улетает всё выше и выше. Здесь ищем верхнюю
    /// опору под сборкой (не дальше полутора клеток) и сажаем предмет на неё.
    /// </summary>
    private bool SettleAssembly(PlacementGrid grid, Rigidbody body)
    {
        Bounds b;
        if (!TryAssemblyBounds(grid, body, out b)) return false;

        const float skin = 0.005f;
        Vector3 half = b.size * 0.5f - new Vector3(skin, skin, skin);
        if (half.x <= 0f || half.y <= 0f || half.z <= 0f) return false;

        var assembly = grid.DragAssemblyBodies;
        var self = new HashSet<Rigidbody>();
        if (assembly != null)
            for (int i = 0; i < assembly.Length; i++)
                if (assembly[i] != null) self.Add(assembly[i]);

        float maxDrop = Mathf.Max(grid.cellSize, 0.1f) * 1.5f;

        var hits = Physics.BoxCastAll(b.center, half, Vector3.down, Quaternion.identity,
            maxDrop, grid.surfaceMask, QueryTriggerInteraction.Ignore);

        float best = -1f;
        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            var col = hit.collider;
            if (col == null) continue;

            var rb = col.attachedRigidbody;
            if (rb != null && self.Contains(rb)) continue;
            if (grid.IsAimBlocker(col)) continue;

            // Опора сверху: пол, стол, полка. Стены (нормаль вбок) — не опора.
            if (hit.normal.y < 0.7f) continue;
            if (hit.distance < 0.005f) continue; // уже стоим на месте
            if (hit.distance > best) best = hit.distance;
        }

        if (best < 0f) return false;

        var drop = new Vector3(0f, -(best - 0.004f), 0f);
        if (!PathClear(grid, body, drop, false)) return false;

        ApplyPin(grid, drop);
        return true;
    }

    /// <summary>
    /// Резко (телепортом) смещает ВСЮ тащимую сборку на дельту — но только
    /// если в целевой позиции она ни с кем не пересекается: стены, перекрытия
    /// и другие предметы принимаются за препятствия. Сил не прикладываем —
    /// значит, ничего не ломаем, не проталкиваем и никуда не пролетаем.
    /// </summary>
    /// <summary>
    /// Проверяет, можно ли сместить всю тащимую сборку на дельту: цель и путь
    /// свободны. Сам ничего не двигает. Маска — surfaceMask (весь мир: стены,
    /// пол, плиты — раньше гейты смотрели только в слой предметов и стены
    /// «не видели», предмет выстреливал за стенку). Игрок и тащимый
    /// игнорируются через IsAimBlocker.
    /// </summary>
    private bool PathClear(PlacementGrid grid, Rigidbody body, Vector3 delta, bool sweep)
    {
        if (delta.sqrMagnitude < 1e-9f) return true;

        Bounds b;
        if (!TryAssemblyBounds(grid, body, out b)) return true;

        var assembly = grid.DragAssemblyBodies;
        var self = new HashSet<Rigidbody>();
        for (int i = 0; i < assembly.Length; i++)
            if (assembly[i] != null) self.Add(assembly[i]);

        const float skin = 0.005f;
        Vector3 half = b.size * 0.5f - new Vector3(skin, skin, skin);
        if (half.x <= 0f || half.y <= 0f || half.z <= 0f) return true;

        // Низ, до которого сборка опустится В РЕЗУЛЬТАТЕ шага: то, что целиком
        // ниже этой линии (+6 см запаса) — «пол», а не препятствие. Раньше свип
        // сравнивал с текущим низом без учёта подъёма, и стол вплотную выше
        // столешницы предмета (например, два разных стола) отсекал шаг с нулевой
        // дистанцией — сборка «не двигалась» вообще.
        float groundY = b.min.y + delta.y + GroundEpsilon;

        // Касания в НАЧАЛЕ пути (мебель вплотную, предмет чуть провален в
        // поверхность): свип стартует из них с дистанцией 0 и отсекает шаг.
        // Решает целевая проверка ниже — она у всех одна.
        HashSet<Collider> startTouch = null;
        if (sweep)
        {
            var start = Physics.OverlapBox(b.center, half, Quaternion.identity,
                grid.surfaceMask, QueryTriggerInteraction.Ignore);
            if (start.Length > 0)
            {
                startTouch = new HashSet<Collider>(start);
            }
        }

        if (sweep)
        {
            Vector3 dir = delta.normalized;
            var path = Physics.BoxCastAll(b.center, half, dir,
                Quaternion.identity, delta.magnitude, grid.surfaceMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < path.Length; i++)
            {
                var hit = path[i];
                var col = hit.collider;
                if (col == null) continue;
                if (startTouch != null && startTouch.Contains(col)) continue;

                var rb = col.attachedRigidbody;
                if (rb != null && self.Contains(rb)) continue;
                if (grid.IsAimBlocker(col)) continue;

                // стоим/опускаемся НА нём (пол/стол/мелкий обломок) — не блок
                if (col.bounds.max.y <= groundY) continue;

                // идём по касательной — не врубаемся
                if (Vector3.Dot(dir, hit.normal) > -0.2f) continue;

                // контакт ровно в цели — это посадка на поверхность, не блок
                if (hit.distance >= delta.magnitude - 0.02f) continue;

                return false; // на пути препятствие
            }
        }

        var hits = Physics.OverlapBox(b.center + delta, half, Quaternion.identity,
            grid.surfaceMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            var col = hits[i];
            if (col == null) continue;

            var rb = col.attachedRigidbody;
            if (rb != null && self.Contains(rb)) continue;
            if (grid.IsAimBlocker(col)) continue;

            // после шага стоим НА нём (пол/стол/перееханный обломок)
            if (col.bounds.max.y <= b.min.y + delta.y + GroundEpsilon) continue;

            return false; // клетка занята
        }

        return true;
    }

    /// <summary>
    /// Смещает всю сборку на дельту (без сил). Двигаем трансформ и сразу
    /// синхронизируем физику. MovePosition оставлял цель «висеть» до следующего
    /// шага физики: если тело в этот момент становилось динамическим (отпустили
    /// кнопку), цель разгоняла его вверх — отсюда «взлетающие» крышки.
    /// </summary>
    private void ApplyPin(PlacementGrid grid, Vector3 delta)
    {
        if (delta.sqrMagnitude < 1e-10f) return;

        var assembly = grid.DragAssemblyBodies;
        if (assembly == null) return;

        for (int i = 0; i < assembly.Length; i++)
        {
            var partRb = assembly[i];
            if (partRb == null) continue;

            partRb.transform.position += delta;
            partRb.velocity = Vector3.zero;
            partRb.angularVelocity = Vector3.zero;
        }

        Physics.SyncTransforms();
    }

    /// <summary>Дистанция вдоль дельты до первого настоящего препятствия.</summary>
    private float NearestBlockDistance(PlacementGrid grid, Rigidbody body, Vector3 delta)
    {
        Bounds b;
        if (!TryAssemblyBounds(grid, body, out b)) return float.MaxValue;

        var assembly = grid.DragAssemblyBodies;
        var self = new HashSet<Rigidbody>();
        for (int i = 0; i < assembly.Length; i++)
            if (assembly[i] != null) self.Add(assembly[i]);

        const float skin = 0.005f;
        Vector3 half = b.size * 0.5f - new Vector3(skin, skin, skin);
        if (half.x <= 0f || half.y <= 0f || half.z <= 0f) return float.MaxValue;

        Vector3 dir = delta.normalized;
        var path = Physics.BoxCastAll(b.center, half, dir,
            Quaternion.identity, delta.magnitude, grid.surfaceMask,
            QueryTriggerInteraction.Ignore);

        float nearest = float.MaxValue;
        for (int i = 0; i < path.Length; i++)
        {
            var hit = path[i];
            var col = hit.collider;
            if (col == null) continue;

            var rb = col.attachedRigidbody;
            if (rb != null && self.Contains(rb)) continue;
            if (grid.IsAimBlocker(col)) continue;
            // «пол» для этого шага — с учётом подъёма/падения, как в PathClear
            if (col.bounds.max.y <= b.min.y + delta.y + GroundEpsilon) continue;
            if (Vector3.Dot(dir, hit.normal) > -0.2f) continue;

            if (hit.distance < nearest) nearest = hit.distance;
        }

        return nearest;
    }

    /// <summary>
    /// Резко по клеткам + адаптивное прислонение: клетка свободна — мгновенно
    /// занимаем; низкая помеха (крышка m2, осколок) — переезжаем ступенькой;
    /// высокое препятствие — скользим вплотную и останавливаемся в паре
    /// миллиметров (прислонить к стене/другому ПК впритык).
    /// </summary>
    private bool TryPinWithClimb(PlacementGrid grid, Rigidbody body, Vector3 delta, bool sweep,
        bool settle = true)
    {
        if (PathClear(grid, body, delta, sweep))
        {
            ApplyPin(grid, delta);
            if (settle) SettleAssembly(grid, body);
            return true;
        }

        float[] climbs = { 0.1f, 0.25f, 0.5f, grid.cellSize };
        for (int i = 0; i < climbs.Length; i++)
        {
            var up = delta;
            up.y += climbs[i];
            if (PathClear(grid, body, up, sweep))
            {
                ApplyPin(grid, up);
                // Подъём «ступенькой» не должен остаться висеть: предмет
                // кинематический и сам не сядет на опору.
                if (settle) SettleAssembly(grid, body);
                return true;
            }
        }

        if (sweep)
        {
            float d = NearestBlockDistance(grid, body, delta);
            if (d < float.MaxValue && d > 0.01f)
            {
                var slide = delta.normalized * Mathf.Max(0f, d - 0.004f);
                ApplyPin(grid, slide);
                if (settle) SettleAssembly(grid, body);
                return true;
            }
        }

        // Ни один вариант не прошёл — оставляем след в логе: кто именно
        // стоит на пути (для разбора «предмет не двигается»).
        LogGridBlocker(grid, body, delta);

        return false;
    }

    /// <summary>Пишет в консоль, какие коллайдеры заблокировали шаг сетки.</summary>
    private void LogGridBlocker(PlacementGrid grid, Rigidbody body, Vector3 delta)
    {
        Bounds b;
        if (!TryAssemblyBounds(grid, body, out b)) return;

        const float skin = 0.005f;
        Vector3 half = b.size * 0.5f - new Vector3(skin, skin, skin);
        if (half.x <= 0f || half.y <= 0f || half.z <= 0f) return;

        var assembly = grid.DragAssemblyBodies;
        var self = new HashSet<Rigidbody>();
        if (assembly != null)
            for (int i = 0; i < assembly.Length; i++)
                if (assembly[i] != null) self.Add(assembly[i]);

        var hits = Physics.OverlapBox(b.center + delta, half, Quaternion.identity,
            grid.surfaceMask, QueryTriggerInteraction.Ignore);

        var names = new List<string>(4);
        float groundY = b.min.y + delta.y + GroundEpsilon;
        for (int i = 0; i < hits.Length && names.Count < 4; i++)
        {
            var col = hits[i];
            if (col == null) continue;

            var rb = col.attachedRigidbody;
            if (rb != null && self.Contains(rb)) continue;
            if (grid.IsAimBlocker(col)) continue;
            if (col.bounds.max.y <= groundY) continue;

            names.Add(col.name + " (maxY=" + col.bounds.max.y + ")");
        }

        if (names.Count == 0) return;

        UnityEngine.Debug.Log(
            "[Grid] шаг " + delta + " заблокирован: " + string.Join(", ", names));
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

        // Состояние сетки — на один перенос: прошлый перенос мог оборваться
        // без End() (цель уничтожена), и тогда старые значения не должны влиять.
        gridMoveInit = false;
        pendingGridMove = Vector3.zero;

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
                        grabOffset = body.transform.TransformDirection(currentDrag.grabOffsetLocal);

                        // Всё в сетке считаем от ЦЕНТРА МАСС: bodySnapped — цель
                        // именно центра масс (ниже из неё вычитается смещение
                        // «центр − пивот»). Раньше здесь брался пивот (body.position),
                        // и каждый кадр предмет сдвигался на −смещение: он «тащился»
                        // в одну сторону и «залетал», особенно если центр далеко от
                        // пивота.
                        Vector3 com = body.worldCenterOfMass;

                        // В режиме сетки предмет ходит ТОЛЬКО стрелками:
                        // горизонталь — текущая позиция + накопленный шаг
                        // стрелок (pendingGridMove), а не точка прицела.
                        // Первый кадр — снап центра масс к ближайшей клетке.
                        if (!gridMoveInit)
                        {
                            var s0 = grid.SnapPosition(com, Vector3.up, null);
                            pendingGridMove += new Vector3(s0.x - com.x, 0f, s0.z - com.z);
                            gridMoveInit = true;
                        }

                        bodySnapped = com + pendingGridMove;
                        pendingGridMove = Vector3.zero;
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

                        // Низ — на поверхности. Линию сетки берём, только если она почти
                        // совпадает с поверхностью. Раньше стол на 0.75 м поднимал предмет
                        // до ближайшей линии сетки (1.0 м): он «левитировал» над столом.
                        float surfaceY = gridHit.point.y;
                        float bottom = surfaceY;
                        if (grid.snapHeight)
                        {
                            float line = grid.SnapCoord(surfaceY);
                            if (Mathf.Abs(line - surfaceY) <= 0.02f) bottom = line;
                        }
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

                        if (!TryPinWithClimb(grid, body, delta, !sunk, false) && hasSurface && normal.y > 0.7f)
                        {
                            // С отснапанной высотой клетка занята — пробуем
                            // встать прямо на поверхность.
                            float bottomOffset = body.worldCenterOfMass.y - AssemblyBoundsMinY(grid, body);
                            bodySnapped.y = gridHit.point.y + bottomOffset;
                            point = bodySnapped - grabOffset;
                            delta = (bodySnapped - comOffset) - body.position;
                            if (!sunk)
                                delta.y = Mathf.Clamp(delta.y, -grid.cellSize, grid.cellSize);
                            TryPinWithClimb(grid, body, delta, !sunk, false);
                        }
                    }

                    DisableSpringForGrid();
                    if (currentDrag != null) currentDrag.gridTouched = true;
                }
                else
                {
                    // Сетку выключили на ходу. Предмет ещё держит резкий снап, а
                    // пружина вот-вот вернётся и потянет его к точке прицела —
                    // на лёгких предметах это рывок «залетает». Поэтому отпускаем.
                    if (currentDrag != null && currentDrag.gridTouched)
                    {
                        End();
                        yield break;
                    }

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

        // OnDragEnded отпускает замок (кинематику) сборки. Вызываем его только
        // когда был ПЕРЕНОС: отпускание кнопки после простого клика-выбора не
        // должно размораживать выбранный предмет — его замок держит ClearSelection.
        if (currentDrag != null && PlacementGrid.Instance != null)
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
                // гасим остаточную скорость от переноса — чтобы на
                // отпускании предмет не выстреливал
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
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
            gridMoveInit = false;
            pendingGridMove = Vector3.zero;

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

        // Предмет всё ещё ВЫБРАН (только закончили перенос) — возвращаем замок
        // выбора: пока включена сетка, физика не должна ни сдвинуть, ни повернуть
        // его и между переносами. OnDragEnded выше его отпустил для чистоты
        // восстановления констрейнтов/скоростей.
        if (selectedBody != null && GridMode())
        {
            var grid = PlacementGrid.Instance;
            if (grid != null) grid.OnDragStarted(selectedBody.transform);
        }
    }
}