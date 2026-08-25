using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Панель аккаунта для мастерской — настраивается через ИНСПЕКТОР.
///
/// Как пользоваться:
///  1. Добавьте этот компонент на любой объект вашей страницы/панели в сцене.
///  2. В инспекторе привяжите свои элементы (поля ввода, кнопки, текст статуса).
///  3. Кнопки в инспекторе (OnClick) подключите к публичным методам:
///        Login()  — войти (берёт nickInput/passInput)
///        Register() — зарегистрироваться
///        Logout() — выйти
///        ToggleForm() — показать/скрыть formRoot
///
/// Если на сцене нет ни одной панели аккаунта, FileInformation создаст
/// одну (AccountPanel.EnsureOnScene).
/// </summary>
public class AccountPanel : MonoBehaviour
{
	[Header("Inspector: привяжите свои элементы")]
	[SerializeField] private InputField nickInput;
	[SerializeField] private InputField passInput;
	[SerializeField] private Button loginButton;
	[SerializeField] private Button registerButton;
	[SerializeField] private Button logoutButton;
	[SerializeField] private Text statusText;
	[Tooltip("Панель, которую показывает ToggleForm (форма входа).")]
	[SerializeField] private GameObject formRoot;

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
		wired = false;
		WireButtons();
		RefreshUI();
	}

	private void WireButtons()
	{
		wired = true;
		if (loginButton != null) loginButton.onClick.AddListener(Login);
		if (registerButton != null) registerButton.onClick.AddListener(Register);
		if (logoutButton != null) logoutButton.onClick.AddListener(Logout);
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
		if (formRoot != null) formRoot.SetActive(!formRoot.activeSelf);
	}

	private void SetStatus(string s)
	{
		if (statusText != null) statusText.text = s;
		Debug.Log("[Account] " + s);
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
