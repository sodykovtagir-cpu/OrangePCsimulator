using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PC.Shop;
using UnityEditor;
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
		int id2 = id;
		if (id == 0) {id2 = GetNewId(item); return;}
		items.Add(id, item);
	}

	public int GetNewId(Item item)
	{
		int id;
		do { id = UnityEngine.Random.Range(int.MinValue, int.MaxValue); }
		while (items.ContainsKey(id));
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

	public void Delivery(GameObject prefab, bool large)
	{
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

	public GameObject InstantDelivery(GameObject prefab)
	{
		if (portal != null)
		{
			portal.SetTrigger("Show");
			if (portalPoint != null)
			{
				return Instantiate(prefab, portalPoint.position, portalPoint.rotation);
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
				Instantiate(original, packagePoint.position, packagePoint.rotation);
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
				throw new InvalidOperationException();
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
		if (raycast == null) return;
		raycast.End();
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
