using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RestoreText : MonoBehaviour, IPointerClickHandler, IEventSystemHandler
{
	private InputField input;
	private string current;
	private string last;
	private bool changed;

	private void Start()
	{
		input = GetComponent<InputField>();
		if (input == null) return;
		input.onValueChanged.AddListener(OnValueChanged);
		input.onEndEdit.AddListener(onEndEdit);
	}

	public void OnPointerClick(PointerEventData eventData)
	{
		current = input.text;
	}

	private void OnValueChanged(string value)
	{
		last = current;
		current = value;
		changed = true;
	}

	private void onEndEdit(string value)
	{
		if (changed && input.wasCanceled)
		{
			input.text = last;
		}
	}

}
