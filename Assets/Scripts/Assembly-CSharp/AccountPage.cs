using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Страница аккаунта (серверная, с двухэтапкой по email).
/// Создаёт себя сама: чип в углу главного меню («Имя» / «Not logged in»),
/// по клику открывает экран:
///   - не вошёл  -> вход/регистрация (имя + пароль + почта) -> код из почты -> вход;
///   - вошёл     -> профиль (имя, почта), управление своими сейвами (удалить/обновить),
///                  настройка: привязать Telegram и получить +5 BTC.
///
/// Зависит от WorkshopClient (сеть) и ServerAccounts (состояние).
/// Чтобы подключить на сцене:  AccountPage.EnsureOnScene()  (или добавь компонент).
/// </summary>
public class AccountPage : MonoBehaviour
{
	[SerializeField] private bool autoCreateUI = true;
	[SerializeField] private Vector2 panelSize = new Vector2(520f, 500f);

	private enum Mode { Login, Register, Verify, Home }
	private Mode mode = Mode.Login;

	private GameObject chip;
	private Text chipText;
	private GameObject page;
	private GameObject loginGroup;
	private GameObject registerGroup;
	private GameObject verifyGroup;
	private GameObject homeGroup;
	private Text statusText;

	// inputs
	private InputField loginField;
	private InputField passwordField;
	private InputField regNameField;
	private InputField regEmailField;
	private InputField regPassField;
	private InputField codeField;
	private InputField tgField;
	private Text nameText;
	private Text emailText;
	private Text bonusText;
	private Transform savesList;

	private static AccountPage instance;
	public static AccountPage Instance { get { return instance; } }

	private void Awake()
	{
		if (instance != null && instance != this) { Destroy(gameObject); return; }
		instance = this;
	}

	private void Start()
	{
		if (autoCreateUI) CreateUI();
		ServerAccounts.StateChanged += OnStateChanged;
		RefreshHomeIfLogged();
	}

	private void OnDestroy()
	{
		ServerAccounts.StateChanged -= OnStateChanged;
	}

	// ================= Public =================

	public void Show()   { if (page != null) page.SetActive(true); Refresh(); }
	public void Hide()   { if (page != null) page.SetActive(false); }
	public void Toggle() { if (page == null) return; if (page.activeSelf) Hide(); else Show(); }

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

	// ================= UI creation =================

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

		// --- Чип в левом верхнем углу ---
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
			chip.GetComponent<Button>().onClick.AddListener(Toggle);
			chipText = MakeText(chip.transform, "Label", "", TextAnchor.MiddleLeft, 14, Color.white);
			chipText.rectTransform.anchorMin = Vector2.zero;
			chipText.rectTransform.anchorMax = Vector2.one;
			chipText.rectTransform.offsetMin = new Vector2(8f, 0f);
			chipText.rectTransform.offsetMax = new Vector2(-8f, 0f);
		}

		// --- Страница (по центру) ---
		if (page == null)
		{
			page = new GameObject("AccountPage", typeof(RectTransform), typeof(Image));
			page.transform.SetParent(parent, false);
			var rt = page.GetComponent<RectTransform>();
			rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
			rt.sizeDelta = panelSize;
			page.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.98f);

			// Заголовок
			var title = MakeText(page.transform, "Title", "Account", TextAnchor.MiddleCenter, 26, new Color(1f, 0.53f, 0f));
			title.rectTransform.anchorMin = new Vector2(0f, 1f);
			title.rectTransform.anchorMax = new Vector2(1f, 1f);
			title.rectTransform.anchoredPosition = new Vector2(0f, -30f);
			title.rectTransform.sizeDelta = new Vector2(0f, 50f);

			// Кнопка закрыть (маленькая, в правом верхнем углу)
			{
				var close = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
				close.transform.SetParent(page.transform, false);
				var crt = close.GetComponent<RectTransform>();
				crt.anchorMin = crt.anchorMax = new Vector2(1f, 1f);
				crt.pivot = new Vector2(1f, 1f);
				crt.anchoredPosition = new Vector2(-10f, -10f);
				crt.sizeDelta = new Vector2(34f, 34f);
				close.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f, 1f);
				var ctx = close.AddComponent<Text>();
				ctx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
				ctx.alignment = TextAnchor.MiddleCenter; ctx.color = Color.white; ctx.fontSize = 16; ctx.text = "X";
				close.GetComponent<Button>().onClick.AddListener(Hide);
			}

			// Статус
			statusText = MakeText(page.transform, "Status", "", TextAnchor.MiddleCenter, 14, Color.white);
			statusText.rectTransform.anchorMin = new Vector2(0f, 1f);
			statusText.rectTransform.anchorMax = new Vector2(1f, 1f);
			statusText.rectTransform.anchoredPosition = new Vector2(0f, -74f);
			statusText.rectTransform.sizeDelta = new Vector2(-40f, 40f);

			loginGroup    = MakeGroup(page.transform, "LoginGroup", new Vector2(0f, 20f));
			registerGroup = MakeGroup(page.transform, "RegisterGroup", new Vector2(0f, 20f));
			verifyGroup   = MakeGroup(page.transform, "VerifyGroup", new Vector2(0f, 20f));
			homeGroup     = MakeGroup(page.transform, "HomeGroup", new Vector2(0f, 60f));

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

		loginField   = MakeInput(g, "LoginField", "Username or email", false, new Vector2(0f, -46f));
		passwordField = MakeInput(g, "PassField", "Password", true, new Vector2(0f, -84f));
		MakeButton(g, "LoginBtn", "Log in", new Vector2(0f, -128f), () => DoLogin());
		MakeButton(g, "ToRegBtn", "No account? Register", new Vector2(0f, -166f), () => SetMode(Mode.Register));
	}

	private void BuildRegister()
	{
		var g = registerGroup.transform;
		MakeText(g, "R1", "Create account", TextAnchor.MiddleCenter, 18, Color.white);

		regNameField  = MakeInput(g, "NameField", "Nickname", false, new Vector2(0f, -46f));
		regEmailField = MakeInput(g, "EmailField", "Email", false, new Vector2(0f, -84f));
		regPassField  = MakeInput(g, "PassField", "Password (min 6)", true, new Vector2(0f, -122f));
		MakeButton(g, "RegBtn", "Register & send code", new Vector2(0f, -166f), () => DoRegister());
		MakeButton(g, "ToLoginBtn", "Back to login", new Vector2(0f, -204f), () => SetMode(Mode.Login));
	}

	private void BuildVerify()
	{
		var g = verifyGroup.transform;
		MakeText(g, "V1", "Check your email", TextAnchor.MiddleCenter, 18, Color.white);
		MakeText(g, "V2", "Enter the 6-digit code sent to your email.", TextAnchor.MiddleCenter, 13, new Color(0.7f, 0.7f, 0.7f), new Vector2(0f, -34f));

		codeField = MakeInput(g, "CodeField", "Code", false, new Vector2(0f, -74f));
		MakeButton(g, "VerifyBtn", "Confirm", new Vector2(0f, -118f), () => DoVerify());
		MakeButton(g, "ResendBtn", "Resend code", new Vector2(0f, -156f), () => DoResend());
		MakeButton(g, "BackBtn", "Back", new Vector2(0f, -194f), () => SetMode(Mode.Login));
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

		MakeText(g, "SavesTitle", "Your saves", TextAnchor.MiddleLeft, 16, Color.white, new Vector2(0f, -84f));

		// Список сейвов
		var listGo = new GameObject("SavesList", typeof(RectTransform));
		listGo.transform.SetParent(g, false);
		var lrt = listGo.GetComponent<RectTransform>();
		lrt.anchorMin = new Vector2(0f, 1f);
		lrt.anchorMax = new Vector2(1f, 1f);
		lrt.anchoredPosition = new Vector2(0f, -130f);
		lrt.sizeDelta = new Vector2(0f, 170f);
		savesList = listGo.transform;

		// Telegram
		tgField = MakeInput(g, "TgField", "@telegram_username", false, new Vector2(-90f, -330f));
		MakeButton(g, "TgBtn", "Link TG + 5 BTC", new Vector2(90f, -330f), () => DoTgLink());

		bonusText = MakeText(g, "Bonus", "", TextAnchor.MiddleCenter, 13, new Color(0.7f, 0.7f, 0.7f), new Vector2(0f, -368f));

		MakeButton(g, "LogoutBtn", "Log out", new Vector2(0f, -412f), DoLogout);
	}

	// ================= Modes =================

	private void SetMode(Mode m)
	{
		mode = m;
		Refresh();
	}

	private void Refresh()
	{
		if (loginGroup == null) return;
		bool logged = ServerAccounts.LoggedIn;
		loginGroup.SetActive(!logged && mode == Mode.Login);
		registerGroup.SetActive(!logged && mode == Mode.Register);
		verifyGroup.SetActive(!logged && mode == Mode.Verify);
		homeGroup.SetActive(logged);

		if (chipText != null)
			chipText.text = logged ? ServerAccounts.Name : "Not logged in";

		if (!logged)
		{
			SetStatus(mode == Mode.Register ? "Create your account with email." :
			          mode == Mode.Verify ? "Code sent to your email." : "Sign in to sync your profile.");
		}
		else
		{
			nameText.text = ServerAccounts.Name;
			emailText.text = ServerAccounts.Email;
			bonusText.text = ServerAccounts.BonusClaimed
				? "Telegram bonus already claimed."
				: "Link your Telegram to get a bonus: +5 BTC";
			RebuildSaves();
		}
	}

	private void RebuildSaves()
	{
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
		var tx = go.AddComponent<Text>();
		tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		tx.alignment = TextAnchor.MiddleCenter; tx.color = Color.white; tx.fontSize = 13; tx.text = label;
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

	private static GameObject MakeGroup(Transform parent, string name, Vector2 center)
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

	private static Button MakeButton(Transform parent, string name, string label, UnityEngine.Events.UnityAction click)
	{
		return MakeButton(parent, name, label, new Vector2(0f, -40f), click);
	}

	private static Button MakeButton(Transform parent, string name, string label, Vector2 pos, UnityEngine.Events.UnityAction click)
	{
		var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
		go.transform.SetParent(parent, false);
		var rt = go.GetComponent<RectTransform>();
		rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
		rt.anchoredPosition = pos;
		rt.sizeDelta = new Vector2(200f, 32f);
		go.GetComponent<Image>().color = new Color(1f, 0.53f, 0f);
		var tx = go.AddComponent<Text>();
		tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		tx.alignment = TextAnchor.MiddleCenter; tx.color = Color.black; tx.fontSize = 14; tx.text = label;
		go.GetComponent<Button>().onClick.AddListener(click);
		return go.GetComponent<Button>();
	}

}

