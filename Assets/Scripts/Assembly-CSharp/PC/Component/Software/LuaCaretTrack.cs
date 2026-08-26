using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PC.Component.Software
{
	public class LuaCaretTrack : MonoBehaviour, IUpdateSelectedHandler
	{
		public int Caret;
		public int SelA;
		public int SelB;
		InputField field;

		void Awake()
		{
			field = GetComponent<InputField>();
		}

		public void Set(int caret, int a, int b)
		{
			Caret = caret;
			SelA = a;
			SelB = b;
		}

		public void OnUpdateSelected(BaseEventData eventData)
		{
			if (field == null) field = GetComponent<InputField>();
			if (field == null) return;
			string t = field.text ?? "";
			int n = t.Length;
			Caret = Mathf.Clamp(field.caretPosition, 0, n);
			SelA = Mathf.Clamp(field.selectionAnchorPosition, 0, n);
			SelB = Mathf.Clamp(field.selectionFocusPosition, 0, n);
		}
	}
}
