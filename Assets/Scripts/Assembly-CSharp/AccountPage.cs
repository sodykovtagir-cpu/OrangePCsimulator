using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Страница аккаунта (серверная, с двухэтапкой по email).
///
/// Два режима:
///  A) АВТО (по умолчанию): autoCreateUI = true и в инспекторе ничего не привязано —
///     весь UI (чип + страница: вход/регистрация/код/профиль) создаётся сам в рантайме.
///  B) ВРУЧНУЮ: привязать элементы ниже в инспекторе. Как только привязан хотя бы
///     один из контейнеров (chipButton / pageRoot / loginPanel / registerPanel /
///     verifyPanel / homePanel), авто-создание ОТКЛЮЧАЕТСЯ — код управляет твоими
///     объектами. Тогда нужно привязать все контейнеры и элементы этой страницы.
///
/// Для ручной сборки:
///   1. Создай в сцене панельки (по одной на каждый экран) и назови их как хочешь.
///   2. Перетащи их в поля ниже.
///   3. Кнопкам можно не вешать onClick в инспекторе — код подпишет их сам
///      (Login/Register/Verify/Resend/LinkTelegram/Logout/Show/LoginTab/RegisterTab),
///      либо вешай эти же публичные методы на onClick вручную.
/// </summary>
public class AccountPage : MonoBehaviour
{
	[Header("Auto create")]
	[Tooltip("Если true и в инспекторе ниже ничего не привязано — UI создастся сам.")]
	[SerializeField] private bool autoCreateUI = true;
	[Tooltip("Размер страницы (только для авто-режима).")]
	[SerializeField] private Vector2 panelSize = new Vector2(520f, 500f);

	// ============ Bindings (свой UI) ============
	[Header("Bindings — чип/кнопка аккаунта (лев-верх)")]
	[Tooltip("Кнопка-чип в углу: показывает имя или 'Not logged in', клик открывает страницу.")]
	[SerializeField] private Button chipButton;
	[Tooltip("Текст внутри чипа (имя / 'Not logged in'). Если пусто — возьмётся из детей chipButton.")]
	[SerializeField] private Text chipText;

	[Header("Bindings — страница (корневая панель)")]
	[SerializeField] private GameObject pageRoot;
	[Tooltip("Кнопка закрытия страницы (X).")]
	[SerializeField] private Button closeButton;
	[Tooltip("Строка статуса вверху страницы.")]
	[SerializeField] private Text statusText;

	[Header("Bindings — экран 'Вход'")]
	[SerializeField] private GameObject loginPanel;
	[SerializeField] private InputField loginField;      // имя или email
	[SerializeField] private InputField passwordField;
	[SerializeField] private Button loginButton;         // -> Login()
	[SerializeField] private Button toRegisterButton;    // -> RegisterTab()

	[Header("Bindings — экран 'Регистрация'")]
	[SerializeField] private GameObject registerPanel;
	[SerializeField] private InputField regNameField;
	[SerializeField] private InputField regEmailField;
	[SerializeField] private InputField regPassField;
	[SerializeField] private Button registerButton;      // -> Register()
	[SerializeField] private Button toLoginButton;       // -> LoginTab()

	[Header("Bindings — экран 'Подтверждение по email'")]
	[SerializeField] private GameObject verifyPanel;
	[SerializeField] private InputField codeField;
	[SerializeField] private Button verifyButton;        // -> Verify()
	[SerializeField] private Button resendButton;        // -> Resend()
	[SerializeField] private Button backButton;          // -> LoginTab()

	[Header("Bindings — экран 'Профиль' (после входа)")]
	[SerializeField] private GameObject homePanel;
	[SerializeField] private Text nameText;
	[SerializeField] private Text emailText;
	[Tooltip("Заголовок над списком сейвов (опционально).")]
	[SerializeField] private Text savesTitle;
	[Tooltip("Контейнер, куда спавнятся строки ваших сейвов (с кнопкой Удалить).")]
	[SerializeField] private Transform savesList;
	[SerializeField] private InputField tgField;         // @telegram_username
	[SerializeField] private Button tgButton;            // -> LinkTelegram()
	[SerializeField] private Text bonusText;
	[SerializeField] private Button logoutButton;        // -> Logout()

	// ============ Внутренние ссылки (авто или из bindings) ============
	private enum Mode { Login, Register, Verify, Home }
	private Mode mode = Mode.Login;

	private GameObject chip;
	private GameObject page;
	private GameObject loginGroup;
	private GameObject registerGroup;
	private GameObject verifyGroup;
	private GameObject homeGroup;

	private bool wired;
	private bool usingBindings;

	private static AccountPage instance;
	public static AccountPage Instance { get { return instance; } }

	private void Awake()
	{
		if (instance != null && instance != this) { Destroy(gameObject); return; }
		instance = this;
	}

	private void Start()
	{
		usingBindings = HasBindings();
		if (autoCreateUI && !usingBindings) CreateUI();
		CommitBindings();
		WireButtons();

		ServerAccounts.StateChanged += OnStateChanged;
		RefreshHomeIfLogged();
	}

	private void OnDestroy()
	{
		ServerAccounts.StateChanged -= OnStateChanged;
	}

	// ================= Public (и для инспектора OnClick) =================

	public void Show()   { if (page != null) page.SetActive(true); Refresh(); }
	public void Hide()   { if (page != null) page.SetActive(false); }
	public void Toggle() { if (page == null) return; if (page.activeSelf) Hide(); else Show(); }

	public void Login()        { DoLogin(); }
	public void Register()     { DoRegister(); }
	public void Verify()       { DoVerify(); }
	public void Resend()       { DoResend(); }
	public void LinkTelegram() { DoTgLink(); }
	public void Logout()       { DoLogout(); }
	public void LoginTab()     { SetMode(Mode.Login); }
	public void RegisterTab()  { SetMode(Mode.Register); }
	public void VerifyTab()    { SetMode(Mode.Verify); }

	/// <summary>Гарантирует наличие панели аккаунта на текущей сцене.</summary>
	public static void EnsureOnScene()
	{
		FindObjectOfType<AccountPage>();
		if (AccountPage.Instance != null) return;
		var go = new GameObject("AccountPage");
		go.AddComponent<AccountPage>();
	}

	private void OnStateChanged()
	{
		Refresh();
	}

	private void RefreshHomeIfLogged()
	{
		if (page != null && ServerAccounts.LoggedIn) Refresh();
	}

	private bool HasBindings()
	{
		return chipButton != null || pageRoot != null || loginPanel != null
			|| registerPanel != null || verifyPanel != null || homePanel != null;
	}

	// ================= Связывание то, что привязано в инспекторе =================

	private void CommitBindings()
	{
		if (!usingBindings) return;

		if (chipButton != null)
		{
			chip = chipButton.gameObject;
			if (chipText == null) chipText = chipButton.GetComponentInChildren<Text>();
		}
		if (pageRoot != null) page = pageRoot;
		loginGroup    = loginPanel;
		registerGroup = registerPanel;
		verifyGroup   = verifyPanel;
		homeGroup     = homePanel;
	}

	private void WireButtons()
	{
		if (wired) return;
		wired = true;

		if (chipButton != null)       { chipButton.onClick.RemoveAllListeners(); chipButton.onClick.AddListener(Toggle); }
		if (closeButton != null)      { closeButton.onClick.RemoveAllListeners(); closeButton.onClick.AddListener(Hide); }
		if (loginButton != null)      { loginButton.onClick.RemoveAllListeners(); loginButton.onClick.AddListener(Login); }
		if (toRegisterButton != null) { toRegisterButton.onClick.RemoveAllListeners(); toRegisterButton.onClick.AddListener(RegisterTab); }
		if (registerButton != null)   { registerButton.onClick.RemoveAllListeners(); registerButton.onClick.AddListener(Register); }
		if (toLoginButton != null)    { toLoginButton.onClick.RemoveAllListeners(); toLoginButton.onClick.AddListener(LoginTab); }
		if (verifyButton != null)     { verifyButton.onClick.RemoveAllListeners(); verifyButton.onClick.AddListener(Verify); }
		if (resendButton != null)     { resendButton.onClick.RemoveAllListeners(); resendButton.onClick.AddListener(Resend); }
		if (backButton != null)       { backButton.onClick.RemoveAllListeners(); backButton.onClick.AddListener(LoginTab); }
		if (tgButton != null)         { tgButton.onClick.RemoveAllListeners(); tgButton.onClick.AddListener(LinkTelegram); }
		if (logoutButton != null)     { logoutButton.onClick.RemoveAllListeners(); logoutButton.onClick.AddListener(Logout); }
	}

	// ================= UI creation (авто-режим) =================

	private Canvas FindCanvas()
	{
		var c = FindObjectOfType<Canvas>();
		if (c == null)
		{
			var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
			c = go.GetComponent<Canvas>();
			c.renderMode = RenderMode.ScreenSpaceOverlay;
			var scaler = go.GetComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.referenceResolution = new Vector2(1920f, 1080f);
			scaler.matchWidthOrHeight = 0.5f;
		}
		EnsureEventSystem();
		return c;
	}

	private static void EnsureEventSystem()
	{
		if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null) return;
		var go = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
	}

	private void CreateUI()
	{
		var canvas = FindCanvas();
		Transform parent = canvas != null ? canvas.transform : transform;

		// --- Чип ---
		if (chip == null)
		{
			chip = new GameObject("AccountChip", typeof(RectTransform), typeof(Image), typeof(Button));
			chip.transform.SetParent(parent, false);
			var rt = chip.GetComponent<RectTransform>();
			rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
			rt.pivot = new Vector2(0f, 1f);
			rt.anchoredPosition = new Vector2(10f, -10f);
			rt.sizeDelta = new Vector2(190f, 32f);
			chip.GetComponent<Image>().color = new Color(0.13f, 0.13f, 0.13f, 0.95f);
			chipButton = chip.GetComponent<Button>();
			chipText = MakeText(chip.transform, "Label", "", TextAnchor.MiddleLeft, 14, Color.white);
			chipText.rectTransform.anchorMin = Vector2.zero;
			chipText.rectTransform.anchorMax = Vector2.one;
			chipText.rectTransform.offsetMin = new Vector2(8f, 0f);
			chipText.rectTransform.offsetMax = new Vector2(-8f, 0f);
		}

		// --- Страница ---
		if (page == null)
		{
			page = new GameObject("AccountPage", typeof(RectTransform), typeof(Image));
			page.transform.SetParent(parent, false);
			var rt = page.GetComponent<RectTransform>();
			rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
			rt.sizeDelta = panelSize;
			page.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.98f);

			var title = MakeText(page.transform, "Title", "Account", TextAnchor.MiddleCenter, 26, new Color(1f, 0.53f, 0f));
			title.rectTransform.anchorMin = new Vector2(0f, 1f);
			title.rectTransform.anchorMax = new Vector2(1f, 1f);
			title.rectTransform.anchoredPosition = new Vector2(0f, -30f);
			title.rectTransform.sizeDelta = new Vector2(0f, 50f);

			// Кнопка закрыть
			{
				var close = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
				close.transform.SetParent(page.transform, false);
				var crt = close.GetComponent<RectTransform>();
				crt.anchorMin = crt.anchorMax = new Vector2(1f, 1f);
				crt.pivot = new Vector2(1f, 1f);
				crt.anchoredPosition = new Vector2(-10f, -10f);
				crt.sizeDelta = new Vector2(34f, 34f);
				close.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f, 1f);
				// Text нельзя вешать на тот же GameObject, что и Image (оба — Graphic).
				// Поэтому подпись создаём дочерним объектом.
				var ctx = MakeText(close.transform, "Label", "X", TextAnchor.MiddleCenter, 16, Color.white);
				var clt = ctx.rectTransform;
				clt.anchorMin = Vector2.zero;
				clt.anchorMax = Vector2.one;
				clt.offsetMin = Vector2.zero;
				clt.offsetMax = Vector2.zero;
				closeButton = close.GetComponent<Button>();
			}

			statusText = MakeText(page.transform, "Status", "", TextAnchor.MiddleCenter, 14, Color.white);
			statusText.rectTransform.anchorMin = new Vector2(0f, 1f);
			statusText.rectTransform.anchorMax = new Vector2(1f, 1f);
			statusText.rectTransform.anchoredPosition = new Vector2(0f, -74f);
			statusText.rectTransform.sizeDelta = new Vector2(-40f, 40f);

			loginGroup    = MakeGroup(page.transform, "LoginGroup");
			registerGroup = MakeGroup(page.transform, "RegisterGroup");
			verifyGroup   = MakeGroup(page.transform, "VerifyGroup");
			homeGroup     = MakeGroup(page.transform, "HomeGroup");

			BuildLogin();
			BuildRegister();
			BuildVerify();
			BuildHome();
		}

		Refresh();
	}

	private void BuildLogin()
	{
		var g = loginGroup.transform;
		MakeText(g, "L1", "Sign in", TextAnchor.MiddleCenter, 18, Color.white);

		loginField     = MakeInput(g, "LoginField", "Username or email", false, new Vector2(0f, -46f));
		passwordField  = MakeInput(g, "PassField", "Password", true, new Vector2(0f, -84f));
		loginButton    = MakeButton(g, "LoginBtn", "Log in", new Vector2(0f, -128f));
		toRegisterButton = MakeButton(g, "ToRegBtn", "No account? Register", new Vector2(0f, -166f));
	}

	private void BuildRegister()
	{
		var g = registerGroup.transform;
		MakeText(g, "R1", "Create account", TextAnchor.MiddleCenter, 18, Color.white);

		regNameField  = MakeInput(g, "NameField", "Nickname", false, new Vector2(0f, -46f));
		regEmailField = MakeInput(g, "EmailField", "Email", false, new Vector2(0f, -84f));
		regPassField  = MakeInput(g, "PassField", "Password (min 6)", true, new Vector2(0f, -122f));
		registerButton = MakeButton(g, "RegBtn", "Register & send code", new Vector2(0f, -166f));
		toLoginButton  = MakeButton(g, "ToLoginBtn", "Back to login", new Vector2(0f, -204f));
	}

	private void BuildVerify()
	{
		var g = verifyGroup.transform;
		MakeText(g, "V1", "Check your email", TextAnchor.MiddleCenter, 18, Color.white);
		MakeText(g, "V2", "Enter the 6-digit code sent to your email.", TextAnchor.MiddleCenter, 13, new Color(0.7f, 0.7f, 0.7f), new Vector2(0f, -34f));

		codeField     = MakeInput(g, "CodeField", "Code", false, new Vector2(0f, -74f));
		verifyButton  = MakeButton(g, "VerifyBtn", "Confirm", new Vector2(0f, -118f));
		resendButton  = MakeButton(g, "ResendBtn", "Resend code", new Vector2(0f, -156f));
		backButton    = MakeButton(g, "BackBtn", "Back", new Vector2(0f, -194f));
	}

	private void BuildHome()
	{
		var g = homeGroup.transform;
		nameText = MakeText(g, "Name", "", TextAnchor.MiddleLeft, 18, new Color(1f, 0.53f, 0f));
		nameText.rectTransform.anchorMin = new Vector2(0f, 1f);
		nameText.rectTransform.anchorMax = new Vector2(1f, 1f);
		nameText.rectTransform.anchoredPosition = new Vector2(0f, -20f);
		nameText.rectTransform.sizeDelta = new Vector2(0f, 24f);

		emailText = MakeText(g, "Email", "", TextAnchor.MiddleLeft, 13, new Color(0.8f, 0.8f, 0.8f));
		emailText.rectTransform.anchorMin = new Vector2(0f, 1f);
		emailText.rectTransform.anchorMax = new Vector2(1f, 1f);
		emailText.rectTransform.anchoredPosition = new Vector2(0f, -44f);
		emailText.rectTransform.sizeDelta = new Vector2(0f, 24f);

		savesTitle = MakeText(g, "SavesTitle", "Your saves", TextAnchor.MiddleLeft, 16, Color.white, new Vector2(0f, -84f));

		var listGo = new GameObject("SavesList", typeof(RectTransform));
		listGo.transform.SetParent(g, false);
		var lrt = listGo.GetComponent<RectTransform>();
		lrt.anchorMin = new Vector2(0f, 1f);
		lrt.anchorMax = new Vector2(1f, 1f);
		lrt.anchoredPosition = new Vector2(0f, -130f);
		lrt.sizeDelta = new Vector2(0f, 170f);
		savesList = listGo.transform;

		tgField = MakeInput(g, "TgField", "@telegram_username", false, new Vector2(-90f, -330f));
		tgButton = MakeButton(g, "TgBtn", "Link TG + 5 BTC", new Vector2(90f, -330f));

		bonusText = MakeText(g, "Bonus", "", TextAnchor.MiddleCenter, 13, new Color(0.7f, 0.7f, 0.7f), new Vector2(0f, -368f));

		logoutButton = MakeButton(g, "LogoutBtn", "Log out", new Vector2(0f, -412f));
	}

	// ================= Modes & Refresh =================

	private void SetMode(Mode m)
	{
		mode = m;
		Refresh();
	}

	private void Refresh()
	{
		if (loginGroup == null && registerGroup == null && verifyGroup == null && homeGroup == null)
			return;

		bool logged = ServerAccounts.LoggedIn;

		if (loginGroup != null)    loginGroup.SetActive(!logged && mode == Mode.Login);
		if (registerGroup != null) registerGroup.SetActive(!logged && mode == Mode.Register);
		if (verifyGroup != null)   verifyGroup.SetActive(!logged && mode == Mode.Verify);
		if (homeGroup != null)     homeGroup.SetActive(logged);

		if (chipText != null)
			chipText.text = logged ? ServerAccounts.Name : "Not logged in";

		if (!logged)
		{
			SetStatus(mode == Mode.Register ? "Create your account with email." :
			          mode == Mode.Verify ? "Code sent to your email." : "Sign in to sync your profile.");
		}
		else
		{
			if (nameText != null)  nameText.text = ServerAccounts.Name;
			if (emailText != null) emailText.text = ServerAccounts.Email;
			if (bonusText != null)
				bonusText.text = ServerAccounts.BonusClaimed
					? "Telegram bonus already claimed."
					: "Link your Telegram to get a bonus: +5 BTC";
			RebuildSaves();
		}
	}

	private void RebuildSaves()
	{
		if (savesList == null) return;
		for (int i = savesList.childCount - 1; i >= 0; i--)
			Destroy(savesList.GetChild(i).gameObject);
		var my = ServerAccounts.MySaves;
		if (my.Count == 0)
		{
			var empty = MakeText(savesList, "Empty", "You haven't published any saves yet.", TextAnchor.MiddleLeft, 13, new Color(0.6f, 0.6f, 0.6f));
			empty.rectTransform.anchorMin = empty.rectTransform.anchorMax = new Vector2(0f, 1f);
			empty.rectTransform.anchoredPosition = new Vector2(0f, -10f);
			return;
		}
		foreach (var s in my)
		{
			var row = new GameObject("row_" + s.id, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(Image));
			row.transform.SetParent(savesList, false);
			row.GetComponent<Image>().color = new Color(0.14f, 0.14f, 0.14f, 1f);
			var h = row.GetComponent<HorizontalLayoutGroup>();
			h.spacing = 6; h.padding = new RectOffset(8, 8, 4, 4); h.childForceExpandWidth = true;

			var title = MakeText(row.transform, "Title", s.title + "  ·  " + s.likes + "♥  " + s.downloads + "⬇", TextAnchor.MiddleLeft, 14, Color.white);
			var le = title.gameObject.AddComponent<LayoutElement>();
			le.flexibleWidth = 1f;

			int id = s.id;
			string owner = s.owner_key;
			MakeRowButton(row.transform, "Del", () => DoDelete(id, owner), 60f);
		}
	}

	private void MakeRowButton(Transform parent, string label, UnityEngine.Events.UnityAction click, float width)
	{
		var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
		go.transform.SetParent(parent, false);
		go.GetComponent<Image>().color = new Color(0.8f, 0.2f, 0.2f, 1f);
		var le = go.AddComponent<LayoutElement>();
		le.preferredWidth = width; le.preferredHeight = 30f;
		// Text — дочерний объект, чтобы не конфликтовать с Image на самой кнопке.
		var tx = MakeText(go.transform, "Label", label, TextAnchor.MiddleCenter, 13, Color.white);
		var trt = tx.rectTransform;
		trt.anchorMin = Vector2.zero;
		trt.anchorMax = Vector2.one;
		trt.offsetMin = Vector2.zero;
		trt.offsetMax = Vector2.zero;
		go.GetComponent<Button>().onClick.AddListener(click);
	}

	// ================= Actions =================

	private void DoLogin()
	{
		string login = loginField != null ? loginField.text.Trim() : "";
		string pass = passwordField != null ? passwordField.text : "";
		if (login == "" || pass == "") { SetStatus("Enter login and password."); return; }
		SetStatus("Signing in...");
		WorkshopClient.Instance.AccountLogin(login, pass, (r, err) =>
		{
			if (err != null) { SetStatus("Network error: " + err); return; }
			if (r == null || !r.ok) { SetStatus(r != null ? Err(r.error) : "Login failed."); return; }
			ServerAccounts.SetSession(r.token, r.name, r.email);
			LoadMe();
			SetStatus("Welcome, " + r.name);
		});
	}

	private void DoRegister()
	{
		string name = regNameField != null ? regNameField.text.Trim() : "";
		string email = regEmailField != null ? regEmailField.text.Trim() : "";
		string pass = regPassField != null ? regPassField.text : "";
		if (name.Length < 3 || email == "" || pass.Length < 6)
		{
			SetStatus("Name ≥3, valid email, password ≥6.");
			return;
		}
		SetStatus("Registering...");
		WorkshopClient.Instance.AccountRegister(name, email, pass, (r, err) =>
		{
			if (err != null) { SetStatus("Network error: " + err); return; }
			if (r == null || !r.ok) { SetStatus(r != null ? Err(r.error) : "Register failed."); return; }
			ServerAccounts.SetSession("", r.name, email);
			recallEmail = email;
			SetMode(Mode.Verify);
			SetStatus(r.sent ? "Code sent! Check your email." : (r.sent == false ? "Code generated (email not sent by host). Enter it below if you have it." : "Verify your email."));
		});
	}

	private string recallEmail = "";

	private void DoVerify()
	{
		string email = recallEmail != "" ? recallEmail : ServerAccounts.Email;
		string code = codeField != null ? codeField.text.Trim() : "";
		if (code == "") { SetStatus("Enter the code from email."); return; }
		SetStatus("Verifying...");
		WorkshopClient.Instance.AccountVerify(email, code, (r, err) =>
		{
			if (err != null) { SetStatus("Network error: " + err); return; }
			if (r == null || !r.ok) { SetStatus(r != null ? Err(r.error) : "Wrong code."); return; }
			ServerAccounts.SetSession(r.token, r.name, r.email);
			recallEmail = "";
			SetStatus("Verified! Welcome, " + r.name);
			LoadMe();
		});
	}

	private void DoResend()
	{
		string email = recallEmail != "" ? recallEmail : ServerAccounts.Email;
		if (email == "") { SetStatus("Enter email first."); return; }
		SetStatus("Sending...");
		WorkshopClient.Instance.AccountResend(email, (r, err) =>
		{
			if (err != null) { SetStatus("Network error: " + err); return; }
			if (r != null && r.ok) SetStatus("Code re-sent. Check your email.");
			else SetStatus("Could not resend.");
		});
	}

	private void LoadMe()
	{
		WorkshopClient.Instance.AccountMe(ServerAccounts.Token, (r, err) =>
		{
			if (r == null || !r.ok) return;
			ServerAccounts.SetSession(ServerAccounts.Token, r.name, r.email);
			if (r.tg_bonus) ServerAccounts.SetBonusClaimed();
			var list = r.saves != null ? new System.Collections.Generic.List<AccountSaveItem>(r.saves) : new System.Collections.Generic.List<AccountSaveItem>();
			ServerAccounts.SetSaves(list);
			Refresh();
		});
	}

	private void DoTgLink()
	{
		string tg = tgField != null ? tgField.text.Trim() : "";
		if (tg == "") { SetStatus("Enter your Telegram username."); return; }
		SetStatus("Linking Telegram...");
		WorkshopClient.Instance.TgLink(ServerAccounts.Token, tg, (r, err) =>
		{
			if (err != null) { SetStatus("Network error: " + err); return; }
			if (r == null || !r.ok) { SetStatus(r != null ? Err(r.error) : "Link failed."); return; }
			if (!string.IsNullOrEmpty(r.link))
			{
				SetStatus("Open in Telegram and press Start: " + r.link);
				return;
			}
			if (r.btc > 0f)
			{
				BitcoinManager.Bitcoin = (float)BitcoinManager.Bitcoin + r.btc;
				ServerAccounts.SetBonusClaimed();
				SetStatus("Linked! Bonus +" + r.btc + " BTC added.");
			}
			else
			{
				SetStatus("Telegram linked.");
			}
			LoadMe();
		});
	}

	private void DoDelete(int id, string owner)
	{
		WorkshopClient.Instance.DeleteSave(id, owner, err =>
		{
			if (err != null) { SetStatus("Delete: " + err); return; }
			SetStatus("Deleted #" + id);
			LoadMe();
		});
	}

	private void DoLogout()
	{
		WorkshopClient.Instance.AccountLogout(ServerAccounts.Token, (err) => { });
		ServerAccounts.Clear();
		SetMode(Mode.Login);
		SetStatus("Logged out");
	}

	private string Err(string code)
	{
		switch (code)
		{
			case "name taken": return "Username already taken.";
			case "email taken": return "Email already registered.";
			case "bad name": return "Nickname must be 3-20 chars.";
			case "bad email": return "Invalid email.";
			case "bad password": return "Password must be ≥6 chars.";
			case "bad code": return "Wrong code.";
			case "expired": return "Code expired. Resend.";
			case "unverified": return "Email not verified yet.";
			case "no session": return "Session expired. Sign in again.";
			case "already": return "Already claimed.";
			default: return code;
		}
	}

	private void SetStatus(string s)
	{
		if (statusText != null) statusText.text = s;
		Debug.Log("[Account] " + s);
	}

	// ================= UI widgets =================

	private static GameObject MakeGroup(Transform parent, string name)
	{
		var go = new GameObject(name, typeof(RectTransform));
		go.transform.SetParent(parent, false);
		var rt = go.GetComponent<RectTransform>();
		rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
		rt.pivot = new Vector2(0.5f, 0.5f);
		rt.anchoredPosition = new Vector2(0f, -40f);
		rt.sizeDelta = new Vector2(420f, 460f);
		return go;
	}

	private static Text MakeText(Transform parent, string name, string content, TextAnchor anchor, int fontSize, Color color)
	{
		var go = new GameObject(name, typeof(RectTransform), typeof(Text));
		go.transform.SetParent(parent, false);
		var tx = go.GetComponent<Text>();
		tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		tx.fontSize = fontSize;
		tx.color = color;
		tx.alignment = anchor;
		tx.text = content;
		tx.raycastTarget = false;
		var rt = go.GetComponent<RectTransform>();
		rt.sizeDelta = new Vector2(380f, 26f);
		return tx;
	}

	private static Text MakeText(Transform parent, string name, string content, TextAnchor anchor, int fontSize, Color color, Vector2 pos)
	{
		var tx = MakeText(parent, name, content, anchor, fontSize, color);
		tx.rectTransform.anchoredPosition = pos;
		return tx;
	}

	private static InputField MakeInput(Transform parent, string name, string placeholder, bool password, Vector2 pos)
	{
		var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
		go.transform.SetParent(parent, false);
		var rt = go.GetComponent<RectTransform>();
		rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
		rt.anchoredPosition = pos;
		rt.sizeDelta = new Vector2(320f, 30f);
		go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.92f);

		var input = go.GetComponent<InputField>();
		input.textComponent = MakeInnerText(go.transform, "Text", Color.black);
		var phText = MakeInnerText(go.transform, "Placeholder", new Color(0.35f, 0.35f, 0.35f));
		input.placeholder = phText;
		phText.text = placeholder;
		if (password) input.contentType = InputField.ContentType.Password;
		return input;
	}

	private static Text MakeInnerText(Transform parent, string name, Color color)
	{
		var go = new GameObject(name, typeof(RectTransform), typeof(Text));
		go.transform.SetParent(parent, false);
		var tx = go.GetComponent<Text>();
		tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		tx.fontSize = 14; tx.color = color; tx.alignment = TextAnchor.MiddleLeft;
		var rt = go.GetComponent<RectTransform>();
		rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
		rt.offsetMin = new Vector2(10f, 2f); rt.offsetMax = new Vector2(-10f, -2f);
		return tx;
	}

	private static Button MakeButton(Transform parent, string name, string label, Vector2 pos)
	{
		var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
		go.transform.SetParent(parent, false);
		var rt = go.GetComponent<RectTransform>();
		rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
		rt.anchoredPosition = pos;
		rt.sizeDelta = new Vector2(200f, 32f);
		go.GetComponent<Image>().color = new Color(1f, 0.53f, 0f);
		// Text — дочерний объект, чтобы не конфликтовать с Image на самой кнопке.
		var tx = MakeText(go.transform, "Label", label, TextAnchor.MiddleCenter, 14, Color.black);
		var trt = tx.rectTransform;
		trt.anchorMin = Vector2.zero;
		trt.anchorMax = Vector2.one;
		trt.offsetMin = Vector2.zero;
		trt.offsetMax = Vector2.zero;
		return go.GetComponent<Button>();
	}
}
