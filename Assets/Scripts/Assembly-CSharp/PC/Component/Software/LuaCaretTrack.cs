using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PC.Component.Software
{
    // Cache selection while the field still has focus. On mobile Unity can reset
    // InputField.caretPosition to zero as soon as a snippet button is touched.
    public class LuaCaretTrack : MonoBehaviour, IUpdateSelectedHandler, IDeselectHandler,
        IPointerUpHandler
    {
        public int Caret { get; private set; }
        public int SelA { get; private set; }
        public int SelB { get; private set; }
        public bool HasSelection { get; private set; }
        private InputField field;

        private void Awake() { field = GetComponent<InputField>(); }

        public void Set(int caret, int a, int b)
        {
            Caret = caret;
            SelA = a;
            SelB = b;
            HasSelection = true;
        }

        private void LateUpdate()
        {
            if (field != null && field.isFocused) Capture();
        }

        public void OnUpdateSelected(BaseEventData eventData) { Capture(); }
        public void OnPointerUp(PointerEventData eventData) { Capture(); }
        public void OnDeselect(BaseEventData eventData)
        {
            // Retain the last focused position; don't read a reset mobile field.
            if (field != null && field.isFocused) Capture();
        }

        public void Capture()
        {
            if (field == null) field = GetComponent<InputField>();
            if (field == null || !field.isFocused) return;
            int n = (field.text ?? "").Length;
            int a = field.selectionAnchorPosition;
            int b = field.selectionFocusPosition;
#if UNITY_ANDROID || UNITY_IOS
            var keyboard = field.touchScreenKeyboard;
            if (keyboard != null && keyboard.active)
            {
                var selection = keyboard.selection;
                a = selection.start;
                b = selection.start + selection.length;
            }
#endif
            SelA = Mathf.Clamp(a, 0, n);
            SelB = Mathf.Clamp(b, 0, n);
            Caret = SelB;
            HasSelection = true;
        }
    }
}
