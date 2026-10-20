using UnityEngine;
using UnityEngine.EventSystems;

namespace PC.Component.Software
{
	public class LuaSnippetButton : MonoBehaviour, IPointerDownHandler
	{
		[Tooltip("Можно пусто — возьмётся LuaEditor с родителя (окно в игре, не префаб).")]
		public LuaEditor editor;
		[TextArea(1, 4)]
		public string insert = "print(\"|\")";

        public void OnPointerDown(PointerEventData eventData)
        {
            var live = GetComponentInParent<LuaEditor>();
            if (live == null || !live.isActiveAndEnabled) live = editor;
            if (live != null) live.CaptureSnippetCaret();
        }

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
