using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

public class SceneSettings : MonoBehaviour
{
	[SerializeField]
	private InputField inputName;

	[SerializeField]
	private Button leftRoomButton;

	[SerializeField]
	private Button rightRoomButton;

	[SerializeField]
	private Text roomText;

	[SerializeField]
	private string[] roomsName;

	private int room;

	private bool gravity = true;

	private bool hardcore = false;

	private static Regex illegalName;

	private void Start()
	{
		room = 0;
		RefreshRoomUI();
	}

	private void OnDestroy()
	{
		inputName.onValueChanged.RemoveListener(OnValueChangedName);
	}

	public void NextRoom()
	{
		room += 1;
		RefreshRoomUI();
	}

	public void PreviousRoom()
	{
		room -= 1;
		RefreshRoomUI();
	}

	private void RefreshRoomUI()
	{
		if (room <= 0)
		{
			leftRoomButton.interactable = false;
		}
		if (!(room >= roomsName.Length - 1))
		{
			rightRoomButton.interactable = true;
		}
		if (room >= roomsName.Length - 1)
		{
			rightRoomButton.interactable = false;
		}
		if (!(room <= 0))
		{
			leftRoomButton.interactable = true;
		}
		RefreshText();
	}

	private void RefreshText()
	{
		roomText.text = roomsName[room];
	}

	public void SetGravity(bool value)
	{
		gravity = value;
	}

	public void SetHardcore(bool value)
	{
		hardcore = value;
	}

	public void OnValueChangedName(string name)
	{
		inputName.text = CheckName(name);
	}

	public void Create()
	{
		if (inputName == null || string.IsNullOrEmpty(inputName.text)) return;

		var path = SaveManagement.SaveUtility.GetNewPath(inputName.text);
		int startingMoney = (!hardcore && PlayerPrefs.GetInt("MoreCoin", 0) == 1) ? 100000 : 2000;

		var data = new GameData
		{
			version = Application.version,
			roomName = inputName.text,
			coin = startingMoney,
			room = room,
			gravity = gravity,
			hardcore = hardcore,
			temperature = 20f,
			ac = false,
			playtime = 0,
			sign = ""
		};

		var loader = new SaveManagement.DataLoader(path, data);
		loader.WriteToFile();
		MainMenu.Instance?.LoadFile(loader);
	}

	public static string CheckName(string name)
	{
		illegalName = new Regex("[" + Regex.Escape(new string(Path.GetInvalidFileNameChars())) + "]", RegexOptions.Compiled);
		return illegalName.Replace(name, "");
	}
}
