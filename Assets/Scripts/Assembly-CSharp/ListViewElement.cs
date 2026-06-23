using UnityEngine;
using UnityEngine.UI;

public class ListViewElement : MonoBehaviour
{
	public Image icon;

	public Text text;

	public Image graphic;

	public Button Button { get; private set; }

	public void Init()
    {
		Button = GetComponent<Button>();
    }
}
