using UnityEngine;

namespace PC.Component.Software
{
	public class LuaSnippetButton : MonoBehaviour
	{
		[Tooltip("Можно пусто — возьмётся LuaEditor с родителя (окно в игре, не префаб).")]
		public LuaEditor editor;
		[TextArea(1, 4)]
		public string insert = "print(\"|\")";

		public void Click()
		{
			var live = GetComponentInParent<LuaEditor>();
			if (live == null || !live.isActiveAndEnabled)
				live = editor;
			if (live != null)
				live.InsertSnippet(insert);
		}
	}
}
