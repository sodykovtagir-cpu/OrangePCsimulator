using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Панель аккаунта для мастерской — настраивается через ИНСПЕКТОР.
///
/// Как пользоваться:
///  1. Добавьте этот компонент на любой объект вашей страницы/панели в сцене.
///  2. В инспекторе привяжите свои элементы (поля ввода, кнопки, текст статуса)
///     или оставьте пустыми — тогда UI создастся автоматически (autoCreateUI).
///  3. Кнопки в инспекторе (OnClick) подключите к публичным методам:
///        Login()  — войти (берёт nickInput/passInput)
///        Register() — зарегистрироваться
///        Logout() — выйти
///        ToggleForm() — показать/скрыть formRoot
///
/// Если на сцене нет ни одной панели аккаунта, FileInformation сам создаст
/// одну (AccountPanel.EnsureOnScene) с авто-UI.
/// </summary>
public class AccountPanel : MonoBehaviour
{
	[Header("Inspector: привяжите свои элементы (иначе авто-UI)")]
	[SerializeField] private InputField nickInput;
	[SerializeField] private InputField passInput;
	[SerializeField] private Button loginButton;
	[SerializeField] private Button registerButton;
	[SerializeField] private Button logoutButton;
	[SerializeField] private Text statusText;
	[Tooltip("Панель, которую показывает ToggleForm (форма входа).")]
	[SerializeField] private GameObject formRoot;

	[Header("Auto UI")]
	[Tooltip("Если true и поля выше не привязаны — кнопка и форма создадутся сами.")]
	[SerializeField] private bool autoCreateUI = true;

	private GameObject autoButton;
	private Text autoButtonText;
	private GameObject autoForm;
	private bool wired;

	private void OnEnable()
	{
		AccountManager.AccountChanged += OnAccountChanged;
		if (!wired) WireButtons();
		RefreshUI();
	}

	private void OnDisable()
	{
		AccountManager.AccountChanged -= OnAccountChanged;
	}

	private void Start()
	{
		if (autoCreateUI && !HasInspectorBindings())
			CreateAutoUI();
		wired = false;
		WireButtons();
		RefreshUI();
	}

	/// <summary>Есть ли пользовательские привязки в инспекторе.</summary>
	private bool HasInspectorBindings()
	{
		return nickInput != null || passInput != null || loginButton != null
			|| registerButton != null || logoutButton != null || formRoot != null;
	}

	private void WireButtons()
	{
		wired = true;
		if (loginButton != null) loginButton.onClick.AddListener(Login);
		if (registerButton != null) registerButton.onClick.AddListener(Register);
		if (logoutButton != null) logoutButton.onClick.AddListener(Logout);
		if (autoButton != null && autoButton.GetComponent<Button>() != null)
			autoButton.GetComponent<Button>().onClick.AddListener(ToggleForm);
	}

	private void OnAccountChanged()
	{
		RefreshUI();
	}

	/// <summary>Обновляет состояние кнопок и текстов по текущему аккаунту.</summary>
	public void RefreshUI()
	{
		bool logged = AccountManager.IsLoggedIn();

		if (loginButton != null && loginButton.gameObject != null)
			loginButton.gameObject.SetActive(!logged);
		if (registerButton != null && registerButton.gameObject != null)
			registerButton.gameObject.SetActive(!logged);
		if (logoutButton != null && logoutButton.gameObject != null)
			logoutButton.gameObject.SetActive(logged);

		if (autoButton != null)
		{
			autoButton.SetActive(true);
			var tx = autoButtonText != null ? autoButtonText : autoButton.GetComponentInChildren<Text>();
			if (tx != null)
				tx.text = logged ? AccountManager.CurrentUser + "  ·  Logout" : "Login / Register";
		}

		if (statusText != null)
			statusText.text = logged
				? "Logged in as " + AccountManager.CurrentUser
				: "Not logged in";
	}

	// ================= Public methods for Inspector OnClick =================

	public void Login()
	{
		string nick = nickInput != null ? nickInput.text : "";
		string pass = passInput != null ? passInput.text : "";
		if (AccountManager.Login(nick, pass))
		{
			if (passInput != null) passInput.text = "";
			if (formRoot != null) formRoot.SetActive(false);
			SetStatus("Logged in as " + AccountManager.CurrentUser);
			RefreshUI();
		}
		else
		{
			SetStatus("Bad login or account not found");
		}
	}

	public void Register()
	{
		string nick = nickInput != null ? nickInput.text : "";
		string pass = passInput != null ? passInput.text : "";
		if (AccountManager.Register(nick, pass))
		{
			if (passInput != null) passInput.text = "";
			if (formRoot != null) formRoot.SetActive(false);
			SetStatus("Registered: " + AccountManager.CurrentUser);
			RefreshUI();
		}
		else
		{
			SetStatus("Name taken or invalid (2-24 chars)");
		}
	}

	public void Logout()
	{
		AccountManager.Logout();
		if (formRoot != null) formRoot.SetActive(false);
		SetStatus("Logged out");
		RefreshUI();
	}

	/// <summary>Показать/скрыть форму входа (formRoot).</summary>
	public void ToggleForm()
	{
		if (AccountManager.IsLoggedIn())
		{
			Logout();
			return;
		}
		if (formRoot == null && autoForm != null) formRoot = autoForm;
		if (formRoot != null) formRoot.SetActive(!formRoot.activeSelf);
	}

	private void SetStatus(string s)
	{
		if (statusText != null) statusText.text = s;
		Debug.Log("[Account] " + s);
	}

	// ================= Auto UI (если ничего не привязано) =================

	private void CreateAutoUI()
	{
		var canvas = FindObjectOfType<Canvas>();
		Transform parent = canvas != null ? canvas.transform : transform;

		// Кнопка в правом верхнем углу
		if (autoButton == null)
		{
			var btn = new GameObject("AccountBtn", typeof(RectTransform), typeof(Image), typeof(Button));
			btn.transform.SetParent(parent, false);
			var rt = btn.GetComponent<RectTransform>();
			rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
			rt.pivot = new Vector2(1f, 1f);
			rt.anchoredPosition = new Vector2(-10f, -10f);
			rt.sizeDelta = new Vector2(170f, 30f);
			btn.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 0.95f);
			btn.GetComponent<Button>().onClick.AddListener(ToggleForm);
			autoButton = btn;

			var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
			label.transform.SetParent(btn.transform, false);
			var tx = label.GetComponent<Text>();
			tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			tx.alignment = TextAnchor.MiddleCenter;
			tx.color = Color.white;
			tx.fontSize = 14;
			autoButtonText = tx;
		}

		// Форма входа
		if (autoForm == null)
		{
			var form = new GameObject("AccountForm", typeof(RectTransform), typeof(Image));
			form.transform.SetParent(parent, false);
			var rt = form.GetComponent<RectTransform>();
			rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
			rt.pivot = new Vector2(1f, 1f);
			rt.anchoredPosition = new Vector2(-10f, -46f);
			rt.sizeDelta = new Vector2(230f, 128f);
			form.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.98f);
			form.SetActive(false);
			autoForm = form;
			formRoot = form;

			var nick = CreateField(form.transform, "NickField", "nick", false, new Vector2(-90f, 48f));
			var pass = CreateField(form.transform, "PassField", "password", true, new Vector2(-90f, 16f));
			nickInput = nick;
			passInput = pass;

			var loginBtn = CreateTextButton(form.transform, "LoginBtn", "Login", new Vector2(-52f, -24f));
			var regBtn = CreateTextButton(form.transform, "RegBtn", "Register", new Vector2(52f, -24f));
			loginButton = loginBtn;
			registerButton = regBtn;
			loginBtn.onClick.AddListener(Login);
			regBtn.onClick.AddListener(Register);
		}

		RefreshUI();
	}

	private static InputField CreateField(Transform parent, string name, string placeholder, bool password, Vector2 pos)
	{
		var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
		go.transform.SetParent(parent, false);
		var rt = go.GetComponent<RectTransform>();
		rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
		rt.pivot = new Vector2(0.5f, 0.5f);
		rt.anchoredPosition = pos;
		rt.sizeDelta = new Vector2(200f, 26f);
		go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.9f);

		var input = go.GetComponent<InputField>();
		var ph = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
		ph.transform.SetParent(go.transform, false);
		var phTx = ph.GetComponent<Text>();
		phTx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		phTx.fontSize = 13;
		phTx.color = new Color(0.3f, 0.3f, 0.3f);
		phTx.text = placeholder;

		var txt = new GameObject("Text", typeof(RectTransform), typeof(Text));
		txt.transform.SetParent(go.transform, false);
		var tx = txt.GetComponent<Text>();
		tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		tx.fontSize = 13;
		tx.color = Color.black;

		input.textComponent = tx;
		input.placeholder = phTx;
		if (password) input.contentType = InputField.ContentType.Password;
		return input;
	}

	private static Button CreateTextButton(Transform parent, string name, string label, Vector2 pos)
	{
		var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
		go.transform.SetParent(parent, false);
		var rt = go.GetComponent<RectTransform>();
		rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
		rt.pivot = new Vector2(0.5f, 0.5f);
		rt.anchoredPosition = pos;
		rt.sizeDelta = new Vector2(96f, 28f);
		go.GetComponent<Image>().color = new Color(1f, 0.53f, 0f);

		var txt = new GameObject("Label", typeof(RectTransform), typeof(Text));
		txt.transform.SetParent(go.transform, false);
		var tx = txt.GetComponent<Text>();
		tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		tx.alignment = TextAnchor.MiddleCenter;
		tx.fontSize = 13;
		tx.color = Color.black;
		tx.text = label;

		return go.GetComponent<Button>();
	}

	// ================= Static helpers =================

	/// <summary>Гарантирует наличие хотя бы одной панели аккаунта на сцене.</summary>
	public static void EnsureOnScene()
	{
		if (FindObjectOfType<AccountPanel>() != null) return;
		var go = new GameObject("AccountPanel");
		go.AddComponent<AccountPanel>();
	}

	/// <summary>Обновляет все панели аккаунта на сцене.</summary>
	public static void RefreshAll()
	{
		foreach (var p in FindObjectsOfType<AccountPanel>())
			if (p != null) p.RefreshUI();
	}
}
