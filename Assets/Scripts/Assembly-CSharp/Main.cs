using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PC.Shop;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(AudioSource))]
public class Main : MonoBehaviour
{
	[SerializeField]
	private MenuManager menuManager;

	[SerializeField]
	private Text infoText;

	[SerializeField]
	private GameObject others;

	[SerializeField]
	private Button pauseButton;

	public bool example;

	public bool hardcore;

	public float playTime;

	[HideInInspector]
	public Raycast raycast;

	private readonly Dictionary<int, Item> items = new Dictionary<int, Item>();

	private AudioSource source;

	private bool showOthers;

	private bool paused;
	public bool IsPaused => paused;

	[Header("Electricity meter (watts)")]
	[SerializeField, Min(0f)] private float roomLightWatts = 100f;
	[SerializeField, Min(0f)] private float airConditionerWatts = 1000f;

	[SerializeField]
	[Header("Money")]
	private Text moneyText;

	[SerializeField]
	private int money = 2000;

	private float oldMoney;

	[Header("Shop")]
	[SerializeField]
	private AudioClip cashSound;

	[SerializeField]
	private AudioClip doorBellSound;

	[SerializeField]
	private Transform packagePoint;

	[SerializeField]
	private Transform portalPoint;

	[SerializeField]
	private Animator portal;

	private Queue<GameObject> orders = new Queue<GameObject>();

	private bool delivering;

	private bool outside;

	[Header("Focus")]
	[SerializeField]
	private GameObject focus;

	private Action unfocusCallback;

	public const float wirelessDelay = 2f;

	public static Main Instance { get; private set; }

	public int Money
	{
		get => money;
		private set
		{
			    if (value > 9999998) value = 9999999;
				if (money != value)
				{
					money = value;
					MoneyChanged?.Invoke();
				}
		}
	}

	public event Action MoneyChanged;

	private void Awake()
	{
		Instance = this;
        if(GetComponent<PC.Component.Software.Sites.SiteHostingService>()==null)gameObject.AddComponent<PC.Component.Software.Sites.SiteHostingService>();
		var grid = GetComponent<ElectricityGrid>();
		if (grid == null) grid = gameObject.AddComponent<ElectricityGrid>();
		grid.ConfigureLoads(roomLightWatts, airConditionerWatts);
		if (GetComponent<PC.Component.Networking.InternetService>() == null)
			gameObject.AddComponent<PC.Component.Networking.InternetService>();
	}

	private void Start()
	{
		source = GetComponent<AudioSource>();
		var p = Player.Instance;
		if (p != null) raycast = p.GetComponentInChildren<Raycast>();
		AudioListener.volume = PlayerPrefs.GetFloat("Volume", 1f);
		if (p != null) p.Sensitivity = PlayerPrefs.GetFloat("Sensitivity", p.Sensitivity);
		if (others != null) showOthers = others.activeSelf;
	}

	private void Update()
	{
		playTime += Time.deltaTime;
		if (Input.GetButtonDown("Cancel"))
		{
			if (unfocusCallback != null) { unfocusCallback(); return; }
			if (others != null && others.activeSelf)
			{
				if (paused) { HidePauseMenu(); return; }
				ShowPauseMenu();
			}
		}
	}

	public void AddItem(int id, Item item)
	{
		if (id == 0) { GetNewId(item); return; }
		if (item == null) return;
		// Индексатор вместо Add: не кидаем исключение при дубликате ID
		// (например, при повторной загрузке сейва со старыми ID).
		items[id] = item;
	}

	public int GetNewId(Item item)
	{
		// Guid вместо Random.Range(int.MinValue, int.MaxValue):
		// у последнего переполняется диапазон (max - min), из-за чего
		// значения схлопываются в узкий/смещённый интервал и растёт
		// число коллизий (вплоть до бесконечного цикла в do/while).
		int id;
		do { id = Guid.NewGuid().GetHashCode(); }
		while (id == 0 || items.ContainsKey(id));
		AddItem(id, item);
		return id;
	}

	public Item GetItemById(int id)
	{
		if (items == null) return null;
		return items.TryGetValue(id, out var item) ? item : null;
	}

	public void SetMoney(int money, bool instant)
	{
		if (money > 9_999_999) money = 9_999_999;

		if (this.money != money)
		{
			this.money = money;
			MoneyChanged?.Invoke();
		}

		if (instant)
		{
			if (moneyText != null) moneyText.text = money.ToString("N0") + "$";
			oldMoney = money;
			return;
		}

		StopCoroutine("MoneyAnimation");
		StartCoroutine("MoneyAnimation");
	}

	public void Spend(int amount)
	{
		if (source != null)
		{
			source.PlayOneShot(cashSound);
			SetMoney(money - amount, false);
		}
	}

	public void AddMoney(int value)
	{
		SetMoney(money + value, false);
	}

	private IEnumerator MoneyAnimation()
	{
		float t = 0f;
		float start = oldMoney;
		while (t < 1f)
		{
			t += Time.deltaTime;
			float cur = Mathf.Lerp(start, money, Mathf.Clamp01(t));
			oldMoney = cur;
			if (moneyText != null) moneyText.text = cur.ToString("N0") + "$";
			yield return null;
		}
		if (moneyText != null) moneyText.text = money.ToString("N0") + "$";
	}

	public void Buy(ShopItem item)
	{
		int price = item.price;
		if (money < price)
		{
			string msg = Localization.GetText("Not enough cash");
			string colored = "<color=red>" + msg + "</color>";
			FadeText(colored);
			return;
		}
		Spend(price);
		Delivery(item.spawn, item.large);
	}

	/// <summary>Доставить покупку обычным путём.</summary>
	public GameObject Delivery(GameObject prefab)
	{
		Delivery(prefab, false);
		return prefab;
	}

	public void Delivery(GameObject prefab, bool large)
	{
		if (prefab == null) return;
		// Обычная (не стеклянная) крышка едет БЕЗ упаковки: она прочная, ящик
		// ей не нужен — в упаковке приезжает только стекло.
		if (IsPlainCover(prefab))
		{
			InstantDelivery(prefab);
			return;
		}
		if (!large && packagePoint != null)
		{
			orders.Enqueue(prefab);
			if (!delivering)
			{
				StartCoroutine(Delivery());
			}
			return;
		}
		InstantDelivery(prefab);
	}

	/// <summary>Событие: в мире только что создали объект доставки.</summary>
	/// <remarks>
	/// Нужно кастомным вещам: рисунок известен лишь после Instantiate, а
	/// выложить предмет из коробки надо раньше, чем игрок её откроет.
	/// </remarks>
	public event Action<GameObject> Spawned;

	/// <summary>
	/// Доставить рисованную вещь (кастомная крышка/стекло) — в её собственной
	/// коробке, как обычная покупка: ящик приезжает к двери, внутри лежит
	/// предмет с уже нанесённым рисунком.
	/// </summary>
	/// <remarks>
	/// Рисунок наносится на созданный экземпляр, поэтому содержимое ящика
	/// собирает код: доставка помечает коробку (Box.ExpectContent), а в момент
	/// появления экземпляра Main собирает предмет и наносит рисунок — до Start()
	/// коробки. Порядок заказов сохраняется очередью, иначе два заказа подряд
	/// перепутали бы рисунки.
	/// </remarks>
	public bool DeliverCustom(GameObject itemPrefab, GameObject boxPrefab, Action<GameObject> configure)
	{
		if (itemPrefab == null) return false;
		if (boxPrefab == null)
		{
			// Коробки нет — отдаём предмет сразу, лишь бы заказ не пропал.
			var direct = NotifySpawned(Instantiate(itemPrefab, DeliveryPosition, DeliveryRotation));
			if (direct == null) return false;
			configure?.Invoke(direct);
			return true;
		}

		customItems.Enqueue(itemPrefab);
		customConfigures.Enqueue(configure);
		customBoxes.Enqueue(boxPrefab);
		EnsureSpawnHook();

		DeliverPackage(boxPrefab);
		return true;
	}

	/// <summary>Предметы кастомных заказов в порядке покупок (см. DeliverCustom).</summary>
	private readonly Queue<GameObject> customItems = new Queue<GameObject>();

	/// <summary>Нанесение рисунка для каждого кастомного заказа, в том же порядке.</summary>
	private readonly Queue<Action<GameObject>> customConfigures = new Queue<Action<GameObject>>();

	/// <summary>Ящики заказов, которые ещё должны появиться (см. DeliverCustom).</summary>
	private readonly Queue<GameObject> customBoxes = new Queue<GameObject>();

	private bool spawnHookInstalled;

	/// <summary>
	/// Одна подписка на все доставки: коробке заказа подкладывается предмет с
	/// рисунком, обычные коробки не трогаем.
	/// </summary>
	private void EnsureSpawnHook()
	{
		if (spawnHookInstalled) return;
		spawnHookInstalled = true;
		Spawned += OnSpawned;
	}

	private void OnSpawned(GameObject spawned)
	{
		if (spawned == null || customConfigures.Count == 0) return;
		// Загрузка сейва: содержимое уже восстановлено отдельной записью.
		if (Box.Restoring) return;
		var box = spawned.GetComponent<Box>();
		if (box == null) return;

		// Каждый заказ доставляется в СВОЁМ ящике: сверяем по префабу, иначе
		// обычная покупка «съела» бы рисунок чужого заказа.
		GameObject expected = customBoxes.Count > 0 ? customBoxes.Peek() : null;
		if (expected == null || !SamePrefab(spawned, expected)) return;
		customBoxes.Dequeue();
		Action<GameObject> configure = customConfigures.Dequeue();

		// Предмет собирается ровно в точке ящика: дальше Box сам подвинет его
		// на своё смещение, как делает с обычным содержимым.
		GameObject itemPrefab = customItems.Count > 0 ? customItems.Dequeue() : null;
		if (itemPrefab == null) return;
		Transform at = spawned.transform;
		box.ExpectContent();
		box.FillWith(() => Instantiate(itemPrefab, at.position, at.rotation), configure);
		// Ящик стекла открывается сразу: рисунок уже на предмете, иначе игрок
		// получил бы стекло без картинки.
		var crate = spawned.GetComponent<Crate>();
		if (crate != null && crate.CanOpen()) crate.BreakApart();
	}

	/// <summary>Экземпляр доставлен из этого префаба?</summary>
	private static bool SamePrefab(GameObject instance, GameObject prefab)
	{
		return instance != null && prefab != null && instance.name.StartsWith(prefab.name);
	}

	/// <summary>
	/// Куда ставить доставку. Раньше при пустом packagePoint заказ молча
	/// пропадал (появлялся лишь варнинг), поэтому теперь есть запасная точка:
	/// у двери, а если и её нет — у портала.
	/// </summary>
	private Vector3 DeliveryPosition
	{
		get
		{
			if (packagePoint != null) return packagePoint.position;
			if (doorPoint != null) return doorPoint.position;
			if (portalPoint != null) return portalPoint.position;
			return transform.position;
		}
	}

	private Quaternion DeliveryRotation
	{
		get
		{
			if (packagePoint != null) return packagePoint.rotation;
			if (doorPoint != null) return doorPoint.rotation;
			if (portalPoint != null) return portalPoint.rotation;
			return transform.rotation;
		}
	}

	[Header("Доставка: запасная точка, если packagePoint не назначен в сцене")]
	[Tooltip("Точка у двери. Используется, когда packagePoint пуст (например, в сценах Main 4/5). Можно оставить пустой — тогда заказ появится у портала.")]
	[SerializeField]
	private Transform doorPoint;

	/// <summary>Отправить коробку той же доставкой, что и купленные детали.</summary>
	public GameObject DeliverPackage(GameObject prefab)
	{
		if (prefab == null) return null;
		if (packagePoint == null)
		{
			// Без точки выгрузки очередь доставки молчала бы, и заказ пропадал.
			// Кладём у doorPoint (если задан), иначе у портала — как обычные
			// предметы, и обязательно через NotifySpawned, чтобы ящик
			// наполнился содержимым (см. Spawned).
			Debug.LogWarning("[Main] packagePoint не назначен — доставка сразу у двери/портала.");
			return NotifySpawned(Instantiate(prefab, DeliveryPosition, DeliveryRotation));
		}
		orders.Enqueue(prefab);
		if (!delivering) StartCoroutine(Delivery());
		return null;
	}

	/// <summary>
	/// Обычная (не стеклянная) крышка: <c>Cover_ATX(Black)</c>,
	/// <c>Cover_ITX(White)</c> и т.п. Стекло в каталоге продаётся ящиком
	/// (<c>Crate_Cover_*</c>), поэтому такие префабы сюда не попадают.
	/// </summary>
	private static bool IsPlainCover(GameObject prefab)
	{
		return prefab != null && prefab.name.StartsWith("Cover_");
	}

	private GameObject NotifySpawned(GameObject spawned)
	{
		if (spawned != null) Spawned?.Invoke(spawned);
		return spawned;
	}

	public GameObject InstantDelivery(GameObject prefab)
	{
		if (portal != null)
		{
			portal.SetTrigger("Show");
			if (portalPoint != null)
			{
				return NotifySpawned(Instantiate(prefab, portalPoint.position, portalPoint.rotation));
			}
		}
		return null;
	}

	private IEnumerator Delivery()
	{
		delivering = true;
		yield return new WaitForSeconds(5f);
		if (source != null) source.PlayOneShot(doorBellSound);

		while (true)
		{
			if (orders == null) yield break;

			if (orders.Count == 0)
			{
				delivering = false;
				yield break;
			}

			var original = orders.Dequeue();
			if (packagePoint != null)
			{
				NotifySpawned(Instantiate(original, packagePoint.position, packagePoint.rotation));
			}

			if (orders.Count == 0)
			{
				delivering = false;
				yield break;
			}

			yield return new WaitForSeconds(0.5f);
		}
	}

	public void EnterVirtualWorld()
	{
		if (!outside)
		{
			StopAllControl();
			StartCoroutine(LoadVirtualWorld());
			outside = true;
		}
	}

	public void ExitVirtualWorld()
	{
		if (outside)
		{
			ResumeAllControl();
			var player = Player.Instance;
			var playerObject = player?.gameObject;
			if (playerObject == null)
			{
				Debug.LogWarning("[Main] Player.Instance is null on ExitVirtualWorld");
				outside = false;
				return;
			}
			playerObject.SetActive(true);
			HideUI(false);
			outside = false;
		}
	}

	public IEnumerator LoadVirtualWorld()
	{
		var op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("VR", UnityEngine.SceneManagement.LoadSceneMode.Additive);
		if (op == null) yield break;
		op.allowSceneActivation = false;
		while (op.progress < 0.9f) yield return null;
		op.allowSceneActivation = true;
		while (!op.isDone) yield return null;

		var p = Player.Instance;
		if (p != null) p.gameObject.SetActive(false);
		HideUI(true);

		var vr = UnityEngine.SceneManagement.SceneManager.GetSceneByName("VR");
		UnityEngine.SceneManagement.SceneManager.SetActiveScene(vr);
	}

	public void Focus(Action ExitPressed)
	{
		StopAllControl();
		HideUI(true);
		if (focus != null)
		{
			focus.SetActive(true);
			unfocusCallback = ExitPressed;
		}
	}

	public void Unfocus()
	{
		unfocusCallback?.Invoke();
	}

	public void OnUnfocus()
	{
		unfocusCallback = null;
		ResumeAllControl();
		HideUI(false);
		focus?.SetActive(false);
	}

	private void HideUI(bool hide)
	{
		if (menuManager != null) menuManager.HideMenu(hide);
		if (showOthers && others != null) others.SetActive(!hide);
	}

	public void FadeText(string text)
	{
		StopCoroutine("Fade");
		StartCoroutine(Fade(text));
	}

	private IEnumerator Fade(string text)
	{
		infoText.text = text;
		infoText.CrossFadeAlpha(1f, 0f, true);
		yield return new WaitForSeconds(1f);
		infoText.CrossFadeAlpha(0f, 1f, true);
	}

	public void StopAllControl()
	{
		// Скрываем панель ДО показа паузы/диалогов: вложенный Canvas иначе
		// рисуется поверх кнопок меню и перехватывает их касания.
		if (DragRotateControls.Instance != null)
			DragRotateControls.Instance.SetVisible(false);
		if (raycast != null) raycast.End();
		var player = Player.Instance;
		if (player != null) player.pauseControls = true;
		#if UNITY_STANDALONE
		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;
		#endif
	}

	public void ResumeAllControl()
	{
		var player = Player.Instance;
		if (player != null) player.pauseControls = false;
		#if UNITY_STANDALONE
		Cursor.lockState = CursorLockMode.Locked;
    	Cursor.visible = false;
		#endif
	}

	public void ShowPauseMenu()
	{
		StopAllControl();
		if (menuManager == null) return;
		menuManager.ShowMenu("Pause");
		menuManager.PlayClickSound();
		paused = true;
	}

	public void HidePauseMenu()
	{
		ResumeAllControl();
		if (menuManager == null) return;
		menuManager.Back();
		menuManager.PlayClickSound();
		paused = false;
	}
}
