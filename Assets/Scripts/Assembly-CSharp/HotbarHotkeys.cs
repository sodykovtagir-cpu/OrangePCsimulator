using UnityEngine;

/// <summary>
/// Позволяет вызывать те же действия, что и кнопки нижней правой панели
/// (Shop, LockRotation, RemoveMode, Zoom, Configuration, AutoRotation, VisualWiring)
/// нажатием цифровых клавиш 1-7 на клавиатуре.
///
/// Как подключить:
/// 1. Положите этот файл в Assets/Scripts/Assembly-CSharp/
/// 2. В сцене Main найдите объект "Panel" (родитель кнопок Shop/Zoom/Configuration и т.д.)
///    - на нём уже висит компонент Functions.
/// 3. Добавьте на этот же объект (или любой другой активный объект сцены) компонент HotbarHotkeys.
/// 4. В инспекторе перетащите объект "Panel" в поле "Functions",
///    а в поле "Menu Manager" — объект, на котором висит MenuManager (обычно тот же Panel/Main canvas).
/// </summary>
public class HotbarHotkeys : MonoBehaviour
{
	[Header("Ссылки")]
	[SerializeField]
	private Functions functions;

	[SerializeField]
	private MenuManager menuManager;

	[Header("Название меню магазина (как в MenuManager.menus)")]
	[SerializeField]
	private string shopMenuName = "Shop";

	private void Reset()
	{
		// Удобство: при первом добавлении скрипта Unity попробует найти ссылки сам.
		if (functions == null) functions = GetComponent<Functions>();
		if (menuManager == null) menuManager = GetComponentInParent<MenuManager>();
	}

	private void Update()
	{
		if (Input.GetKeyDown(KeyCode.Alpha1)) OnSlot1_Shop();
		if (Input.GetKeyDown(KeyCode.Alpha2)) OnSlot2_LockRotation();
		if (Input.GetKeyDown(KeyCode.Alpha3)) OnSlot3_RemoveMode();
		if (Input.GetKeyDown(KeyCode.Alpha4)) OnSlot4_Zoom();
		if (Input.GetKeyDown(KeyCode.Alpha5)) OnSlot5_Configuration();
		if (Input.GetKeyDown(KeyCode.Alpha6)) OnSlot6_AutoRotation();
		if (Input.GetKeyDown(KeyCode.Alpha7)) OnSlot7_VisualWiring();
	    if (Input.GetKeyDown(KeyCode.Alpha8)) OnSlot8_Earn();
	}

	public void OnSlot1_Shop()
	{
		if (menuManager != null)
		{
			menuManager.ShowMenu(shopMenuName);
			menuManager.PlayClickSound();
		}
		else
		{
			Debug.LogWarning("HotbarHotkeys: MenuManager не назначен, кнопка Shop (1) не сработает.");
		}
	}

	public void OnSlot2_LockRotation()
	{
		if (functions != null) functions.LockRotation();
	}

	public void OnSlot3_RemoveMode()
	{
		if (functions != null) functions.RemoveMode();
	}

	public void OnSlot4_Zoom()
	{
		if (functions != null) functions.Zoom();
	}

	public void OnSlot5_Configuration()
	{
		if (functions != null) functions.Configuration();
	}

	public void OnSlot6_AutoRotation()
	{
		if (functions != null) functions.AutoRotation();
	}

	public void OnSlot7_VisualWiring()
	{
		if (functions != null) functions.VisualWiring();
	}

private void OnSlot8_Earn()
{
    var menu = FindObjectOfType<MenuManager>();

    if (menu != null)
    {
        menu.ShowMenu("Earn");
    }
}
}
