using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Страница аккаунта (серверная, с двухэтапкой по email).
///
/// Логика:
///   • При старте сцены на чипе сразу ставится имя / "Not loggined".
///   • Клик по чипу: скрывает Main, включает AccountPage и нужный экран
///     (Login если не вошёл, Profile если вошёл).
///   • Кнопка Закрыть на каждой странице: выключает AccountPage, включает Main.
///   • На EmailVerification кнопка Назад возвращает к предыдущему шагу.
/// </summary>
public class AccountPage : MonoBehaviour
{
	private const string NotLoggedChip = "Not loggined";

	// ============ Bindings (свой UI) ============
	[Header("Bindings — чип/кнопка аккаунта (лев-верх)")]
	[Tooltip("Кнопка-чип в углу: показывает имя или 'Not loggined', клик открывает страницу.")]
	[SerializeField] private Button chipButton;
	[Tooltip("Текст внутри чипа (имя / 'Not loggined'). Если пусто — возьмётся из детей chipButton.")]
	[SerializeField] private Text chipText;

	[Header("Bindings — страница (корневая панель)")]
	[SerializeField] private GameObject pageRoot;
	[Tooltip("Панель главного меню (объект Main). Прячется при открытии аккаунта.")]
	[SerializeField] private GameObject mainPanel;
	[Tooltip("Общая кнопка закрытия страницы (X). Можно не назначать, если есть кнопки на каждом экране.")]
	[SerializeField] private Button closeButton;
	[Tooltip("Строка статуса вверху страницы.")]
	[SerializeField] private Text statusText;

	[Header("Bindings — экран 'Вход'")]
	[SerializeField] private GameObject loginPanel;
	[SerializeField] private InputField loginField;      // имя или email
	[SerializeField] private InputField passwordField;
	[SerializeField] private Button loginButton;         // -> Login()
	[SerializeField] private Button toRegisterButton;    // -> RegisterTab()
	[SerializeField] private Button loginCloseButton;    // -> Hide()

	[Header("Bindings — экран 'Регистрация'")]
	[SerializeField] private GameObject registerPanel;
	[SerializeField] private InputField regNameField;
	[SerializeField] private InputField regEmailField;
	[SerializeField] private InputField regPassField;
	[SerializeField] private Button registerButton;      // -> Register()
	[SerializeField] private Button toLoginButton;       // -> LoginTab()
	[SerializeField] private Button registerCloseButton; // -> Hide()

	[Header("Bindings — экран 'Подтверждение по email'")]
	[SerializeField] private GameObject verifyPanel;
	[SerializeField] private InputField codeField;
	[SerializeField] private Button verifyButton;        // -> Verify()
	[SerializeField] private Button resendButton;        // -> Resend()
	[SerializeField] private Button backButton;          // -> предыдущий шаг
	[SerializeField] private Button verifyCloseButton;   // -> Hide()

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
	[SerializeField] private Button homeCloseButton;     // -> Hide()

	// ============ Внутренние ссылки (авто или из bindings) ============
	private enum Mode { Login, Register, Verify, Home }
	private Mode mode = Mode.Login;
	private Mode verifyReturnMode = Mode.Register;

	private GameObject chip;
	private GameObject page;
	private GameObject loginGroup;
	private GameObject registerGroup;
	private GameObject verifyGroup;
	private GameObject homeGroup;

	private bool wired;
	private bool subscribed;
	private bool opened;

	private static AccountPage instance;
	public static AccountPage Instance { get { return instance; } }

	private void Awake()
	{
		if (instance != null && instance != this) { Destroy(gameObject); return; }
		instance = this;
	}

	private void Start()
	{
		Prepare();
	}

	private void OnDestroy()
	{
		if (subscribed)
		{
			ServerAccounts.StateChanged -= OnStateChanged;
			subscribed = false;
		}
		if (instance == this) instance = null;
	}

	/// <summary>
	/// Можно вызвать с неактивного объекта (MainMenu при старте сцены):
	/// ставит имя на чип и подписывает кнопку, не включая AccountPage.
	/// </summary>
	public void Prepare()
	{
		if (instance == null || instance == this) instance = this;

		CommitBindings();
		ResolveMain();
		WireButtons();
		Subscribe();
		RefreshChip();
		WorkshopClient.Ensure();
	}

	// ================= Public (и для инспектора OnClick) =================

	public void Show()
	{
		opened = true;
		if (mainPanel != null) mainPanel.SetActive(false);
		if (page != null) page.SetActive(true);

		if (ServerAccounts.LoggedIn) SetMode(Mode.Home);
		else SetMode(Mode.Login);
	}

	public void Hide()
	{
		opened = false;
		if (page != null) page.SetActive(false);
		if (mainPanel != null) mainPanel.SetActive(true);
		RefreshChip();
	}

	public void Toggle()
	{
		if (page != null && page.activeSelf) Hide();
		else Show();
	}

	public void Login()        { DoLogin(); }
	public void Register()     { DoRegister(); }
	public void Verify()       { DoVerify(); }
	public void Resend()       { DoResend(); }
	public void LinkTelegram() { DoTgLink(); }
	public void Logout()       { DoLogout(); }
	public void LoginTab()     { SetMode(Mode.Login); }
	public void RegisterTab()  { SetMode(Mode.Register); }
	public void VerifyTab()    { EnterVerify(mode); }
	public void BackFromVerify()
	{
		Mode target = verifyReturnMode;
		if (target == Mode.Verify || target == Mode.Home) target = Mode.Register;
		SetMode(target);
	}

	/// <summary>Гарантирует наличие панели аккаунта на текущей сцене и готовит чип.</summary>
	public static void EnsureOnScene()
	{
		AccountPage found = FindExisting();
		if (found == null)
		{
			var go = new GameObject("AccountPage");
			found = go.AddComponent<AccountPage>();
		}
		found.enabled = true;
		found.Prepare();
	}

	private static AccountPage FindExisting()
	{
		var all = Resources.FindObjectsOfTypeAll<AccountPage>();
		if (all != null)
		{
			for (int i = 0; i < all.Length; i++)
			{
				var p = all[i];
				if (p == null) continue;
				if (!p.gameObject.scene.IsValid()) continue;
				return p;
			}
		}
		return FindObjectOfType<AccountPage>();
	}

	private void OnStateChanged()
	{
		RefreshChip();
		if (opened || (page != null && page.activeSelf)) Refresh();
	}

	// ================= Связывание то, что привязано в инспекторе =================

	private void CommitBindings()
	{
		if (chipButton != null)
		{
			chip = chipButton.gameObject;
			if (chipText == null) chipText = chipButton.GetComponentInChildren<Text>();
		}
		if (pageRoot != null) page = pageRoot;
		else if (page == null && gameObject.name == "AccountPage") page = gameObject;

		loginGroup    = loginPanel;
		registerGroup = registerPanel;
		verifyGroup   = verifyPanel;
		homeGroup     = homePanel;
	}

	private void ResolveMain()
	{
		if (mainPanel != null) return;
		var found = GameObject.Find("Main");
		if (found != null) mainPanel = found;
	}

	private void Subscribe()
	{
		if (subscribed) return;
		ServerAccounts.StateChanged += OnStateChanged;
		subscribed = true;
	}

	private void WireButtons()
	{
		if (wired) return;
		wired = true;

		if (chipButton != null)
		{
			chipButton.onClick.RemoveAllListeners();
			chipButton.onClick.AddListener(Show);
		}

		BindClose(closeButton);
		BindClose(loginCloseButton);
		BindClose(registerCloseButton);
		BindClose(verifyCloseButton);
		BindClose(homeCloseButton);

		if (loginButton != null)      { loginButton.onClick.RemoveAllListeners(); loginButton.onClick.AddListener(Login); }
		if (toRegisterButton != null) { toRegisterButton.onClick.RemoveAllListeners(); toRegisterButton.onClick.AddListener(RegisterTab); }
		if (registerButton != null)   { registerButton.onClick.RemoveAllListeners(); registerButton.onClick.AddListener(Register); }
		if (toLoginButton != null)    { toLoginButton.onClick.RemoveAllListeners(); toLoginButton.onClick.AddListener(LoginTab); }
		if (verifyButton != null)     { verifyButton.onClick.RemoveAllListeners(); verifyButton.onClick.AddListener(Verify); }
		if (resendButton != null)     { resendButton.onClick.RemoveAllListeners(); resendButton.onClick.AddListener(Resend); }
		if (backButton != null)
		{
			DisableMenuBack(backButton);
			backButton.onClick.RemoveAllListeners();
			backButton.onClick.AddListener(BackFromVerify);
		}
		if (tgButton != null)         { tgButton.onClick.RemoveAllListeners(); tgButton.onClick.AddListener(LinkTelegram); }
		if (logoutButton != null)     { logoutButton.onClick.RemoveAllListeners(); logoutButton.onClick.AddListener(Logout); }

		// Лишние [Return]/Закрыть на экранах, которые ещё не назначены вручную.
		WireExtraCloses(loginGroup, loginButton, toRegisterButton, loginCloseButton);
		WireExtraCloses(registerGroup, registerButton, toLoginButton, registerCloseButton);
		// На Verify [Return] — это Назад, не закрытие.
		WireExtraCloses(verifyGroup, verifyButton, resendButton, backButton, verifyCloseButton);
		WireExtraCloses(homeGroup, tgButton, logoutButton, homeCloseButton);
	}

	private void BindClose(Button btn)
	{
		if (btn == null) return;
		DisableMenuBack(btn);
		btn.onClick.RemoveAllListeners();
		btn.onClick.AddListener(Hide);
	}

	private static void DisableMenuBack(Button btn)
	{
		if (btn == null) return;
		var change = btn.GetComponent<ChangeMenu>();
		if (change != null) change.enabled = false;
	}

	private void WireExtraCloses(GameObject panel, params Button[] reserved)
	{
		if (panel == null) return;
		var buttons = panel.GetComponentsInChildren<Button>(true);
		for (int i = 0; i < buttons.Length; i++)
		{
			var b = buttons[i];
			if (b == null) continue;
			bool skip = false;
			for (int r = 0; r < reserved.Length; r++)
			{
				if (reserved[r] == b) { skip = true; break; }
			}
			if (skip) continue;
			if (!LooksLikeClose(b)) continue;
			BindClose(b);
		}
	}

	private static bool LooksLikeClose(Button b)
	{
		if (LooksLikeCloseText(b.gameObject.name)) return true;
		var tx = b.GetComponentInChildren<Text>();
		return tx != null && LooksLikeCloseText(tx.text);
	}

	private static bool LooksLikeCloseText(string s)
	{
		if (string.IsNullOrEmpty(s)) return false;
		s = s.ToLowerInvariant();
		return s.Contains("close") || s.Contains("закры") || s.Contains("return");
	}

	// ================= Modes & Refresh =================

	private void EnterVerify(Mode from)
	{
		if (from != Mode.Verify) verifyReturnMode = from;
		if (verifyReturnMode == Mode.Home || verifyReturnMode == Mode.Verify)
			verifyReturnMode = Mode.Register;
		SetMode(Mode.Verify);
	}

	private void SetMode(Mode m)
	{
		mode = m;
		Refresh();
	}

	private void RefreshChip()
	{
		if (chipText == null && chipButton != null)
			chipText = chipButton.GetComponentInChildren<Text>();
		if (chipText == null) return;
		chipText.text = ServerAccounts.LoggedIn ? ServerAccounts.Name : NotLoggedChip;
	}

	private void Refresh()
	{
		RefreshChip();

		bool logged = ServerAccounts.LoggedIn;

		if (loginGroup != null)    loginGroup.SetActive(!logged && mode == Mode.Login);
		if (registerGroup != null) registerGroup.SetActive(!logged && mode == Mode.Register);
		if (verifyGroup != null)   verifyGroup.SetActive(!logged && mode == Mode.Verify);
		if (homeGroup != null)     homeGroup.SetActive(logged && mode == Mode.Home);

		if (!logged)
		{
			SetStatus(mode == Mode.Register ? "Create your account with email." :
			          mode == Mode.Verify ? "Code sent to your email." : "Sign in to sync your profile.");
			return;
		}

		if (nameText != null)  nameText.text = ServerAccounts.Name;
		if (emailText != null) emailText.text = ServerAccounts.Email;
		if (bonusText != null)
			bonusText.text = ServerAccounts.BonusClaimed
				? "Telegram bonus already claimed."
				: "Link your Telegram to get a bonus: +5 BTC";
		RebuildSaves();
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
		var tx = MakeText(go.transform, "Label", label, TextAnchor.MiddleCenter, 13, Color.white);
		var trt = tx.rectTransform;
		trt.anchorMin = Vector2.zero;
		trt.anchorMax = Vector2.one;
		trt.offsetMin = Vector2.zero;
		trt.offsetMax = Vector2.zero;
		go.GetComponent<Button>().onClick.AddListener(click);
	}

	// ================= Actions =================

	private static string FieldText(InputField field)
	{
		if (field == null || field.text == null) return "";
		return field.text.Trim();
	}

	private WorkshopClient Net()
	{
		var wc = WorkshopClient.Ensure();
		if (wc == null)
		{
			SetStatus("Network client missing.");
			return null;
		}
		return wc;
	}

	private void DoLogin()
	{
		string login = FieldText(loginField);
		string pass = passwordField != null && passwordField.text != null ? passwordField.text : "";
		if (login == "" || pass == "") { SetStatus("Enter login and password."); return; }
		var wc = Net();
		if (wc == null) return;
		SetStatus("Signing in...");
		wc.AccountLogin(login, pass, (r, err) =>
		{
			if (err != null) { SetStatus("Network error: " + err); return; }
			if (r == null || !r.ok) { SetStatus(r != null ? Err(r.error) : "Login failed."); return; }
			ServerAccounts.SetSession(r.token, r.name, r.email);
			LoadMe();
			SetMode(Mode.Home);
			SetStatus("Welcome, " + r.name);
		});
	}

	private void DoRegister()
	{
		string name = FieldText(regNameField);
		string email = FieldText(regEmailField);
		string pass = regPassField != null && regPassField.text != null ? regPassField.text : "";
		if (name.Length < 3 || email == "" || pass.Length < 6)
		{
			SetStatus("Name ≥3, valid email, password ≥6.");
			return;
		}
		var wc = Net();
		if (wc == null) return;
		SetStatus("Registering...");
		wc.AccountRegister(name, email, pass, (r, err) =>
		{
			if (err != null) { SetStatus("Network error: " + err); return; }
			if (r == null || !r.ok) { SetStatus(r != null ? Err(r.error) : "Register failed."); return; }
			ServerAccounts.SetSession("", r.name, email);
			recallEmail = email;
			EnterVerify(Mode.Register);
			SetStatus(r.sent ? "Code sent! Check your email." : (r.sent == false ? "Code generated (email not sent by host). Enter it below if you have it." : "Verify your email."));
		});
	}

	private string recallEmail = "";

	private void DoVerify()
	{
		string email = recallEmail != "" ? recallEmail : ServerAccounts.Email;
		string code = FieldText(codeField);
		if (code == "") { SetStatus("Enter the code from email."); return; }
		var wc = Net();
		if (wc == null) return;
		SetStatus("Verifying...");
		wc.AccountVerify(email, code, (r, err) =>
		{
			if (err != null) { SetStatus("Network error: " + err); return; }
			if (r == null || !r.ok) { SetStatus(r != null ? Err(r.error) : "Wrong code."); return; }
			ServerAccounts.SetSession(r.token, r.name, r.email);
			recallEmail = "";
			SetStatus("Verified! Welcome, " + r.name);
			SetMode(Mode.Home);
			LoadMe();
		});
	}

	private void DoResend()
	{
		string email = recallEmail != "" ? recallEmail : ServerAccounts.Email;
		if (email == "") { SetStatus("Enter email first."); return; }
		var wc = Net();
		if (wc == null) return;
		SetStatus("Sending...");
		wc.AccountResend(email, (r, err) =>
		{
			if (err != null) { SetStatus("Network error: " + err); return; }
			if (r != null && r.ok) SetStatus("Code re-sent. Check your email.");
			else SetStatus("Could not resend.");
		});
	}

	private void LoadMe()
	{
		var wc = Net();
		if (wc == null) return;
		wc.AccountMe(ServerAccounts.Token, (r, err) =>
		{
			if (r == null || !r.ok) return;
			ServerAccounts.SetSession(ServerAccounts.Token, r.name, r.email);
			if (r.tg_bonus) ServerAccounts.SetBonusClaimed();
			var list = r.saves != null ? new System.Collections.Generic.List<AccountSaveItem>(r.saves) : new System.Collections.Generic.List<AccountSaveItem>();
			ServerAccounts.SetSaves(list);
			if (opened || (page != null && page.activeSelf)) Refresh();
			else RefreshChip();
		});
	}

	private void DoTgLink()
	{
		string tg = FieldText(tgField);
		if (tg == "") { SetStatus("Enter your Telegram username."); return; }
		var wc = Net();
		if (wc == null) return;
		SetStatus("Linking Telegram...");
		wc.TgLink(ServerAccounts.Token, tg, (r, err) =>
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
		var wc = Net();
		if (wc == null) return;
		wc.DeleteSave(id, owner, err =>
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
		RefreshChip();
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
}
