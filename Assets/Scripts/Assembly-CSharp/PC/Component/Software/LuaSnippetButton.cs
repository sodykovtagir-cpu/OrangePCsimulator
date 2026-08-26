using UnityEngine;

namespace PC.Component.Software
{
	public class LuaSnippetButton : MonoBehaviour
	{
		public LuaEditor editor;
		[TextArea(1, 4)]
		public string insert = "print(\"|\")";

		public void Click()
		{
			if (editor == null) editor = GetComponentInParent<LuaEditor>();
			if (editor != null) editor.InsertSnippet(insert);
		}
	}
}
