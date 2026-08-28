using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
    /// <summary>
    /// InputField с поведением как в Блокноте Windows:
    /// - Растёт по высоте пока не достигнет maxHeight
    /// - После этого включает вертикальный скролл
    /// - Плавная прокрутка колесом и стрелками
    /// - Автопрокрутка при вводе внизу
    /// </summary>
    [RequireComponent(typeof(InputField))]
    [RequireComponent(typeof(ScrollRect))]
    public class NotepadInput : MonoBehaviour
    {
        [Header("Размеры")]
        public float minHeight = 24f;
        public float maxHeight = 400f;
        public float lineHeight = 18f;
        public float padding = 4f;

        [Header("Ссылки")]
        public InputField inputField;
        public Text textComponent;
        public ScrollRect scrollRect;
        public RectTransform contentRect;

        private RectTransform selfRect;
        private string lastText = "";
        private float currentContentHeight;
        private bool isScrolling;
        private int lastCaretPos;

        void Awake()
        {
            if (inputField == null) inputField = GetComponent<InputField>();
            if (textComponent == null && inputField != null) textComponent = inputField.textComponent;
            if (scrollRect == null) scrollRect = GetComponent<ScrollRect>();
            if (contentRect == null && textComponent != null) contentRect = textComponent.rectTransform;
            selfRect = GetComponent<RectTransform>();
        }

        void OnEnable()
        {
            if (inputField != null)
            {
                inputField.onValueChanged.AddListener(OnTextChanged);
            }
            CalculateLayout();
        }

        void OnDisable()
        {
            if (inputField != null)
                inputField.onValueChanged.RemoveListener(OnTextChanged);
        }

        void OnTextChanged(string newText)
        {
            if (newText == lastText) return;

            lastCaretPos = inputField != null ? inputField.caretPosition : 0;
            lastText = newText;

            CalculateLayout();

            // Автоскролл вниз если каретка в конце
            if (inputField != null && isScrolling)
            {
                int textLen = newText != null ? newText.Length : 0;
                if (lastCaretPos >= textLen - 1)
                {
                    scrollRect.verticalNormalizedPosition = 0f;
                }
            }
        }

        void CalculateLayout()
        {
            if (textComponent == null || selfRect == null) return;

            string text = textComponent.text ?? "";
            float contentHeight = CalculateTextHeight(text);
            currentContentHeight = contentHeight;

            float newHeight = Mathf.Clamp(contentHeight, minHeight, maxHeight);
            isScrolling = contentHeight > maxHeight;

            // Обновляем размер InputField
            var size = selfRect.sizeDelta;
            if (Mathf.Abs(size.y - newHeight) > 1f)
            {
                selfRect.sizeDelta = new Vector2(size.x, newHeight);
            }

            // Настраиваем скролл
            if (scrollRect != null)
            {
                scrollRect.vertical = isScrolling;
                scrollRect.horizontal = false;

                if (contentRect != null)
                {
                    // Контент должен быть высотой с текст
                    var cSize = contentRect.sizeDelta;
                    contentRect.sizeDelta = new Vector2(cSize.x, Mathf.Max(contentHeight, newHeight));
                }
            }

            // Восстанавливаем каретку
            if (inputField != null)
            {
                inputField.caretPosition = Mathf.Min(lastCaretPos, text.Length);
                inputField.selectionAnchorPosition = inputField.caretPosition;
                inputField.selectionFocusPosition = inputField.caretPosition;
            }
        }

        float CalculateTextHeight(string text)
        {
            if (string.IsNullOrEmpty(text)) return lineHeight + padding * 2;

            float width = selfRect != null ? selfRect.rect.width - padding * 2 : 200f;
            float charWidth = textComponent != null ? textComponent.fontSize * 0.6f : 10f;
            float charsPerLine = width / charWidth;
            if (charsPerLine <= 0) charsPerLine = 1;

            string[] lines = text.Split('\n');
            int totalLines = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int wrappedLines = 1;
                float lineWidth = 0;

                for (int j = 0; j < line.Length; j++)
                {
                    char c = line[j];
                    float cw = c == '\t' ? charWidth * 4f : (c == ' ' ? charWidth * 0.5f : charWidth);
                    lineWidth += cw;
                    if (lineWidth > width)
                    {
                        wrappedLines++;
                        lineWidth = cw;
                    }
                }
                totalLines += wrappedLines;
            }

            return totalLines * lineHeight + padding * 2;
        }

        // Прокрутка колесом мыши
        void OnScrollWheel(float delta)
        {
            if (!isScrolling || scrollRect == null) return;

            float scrollSpeed = 20f;
            float pos = scrollRect.verticalNormalizedPosition;
            pos += delta * scrollSpeed / currentContentHeight;
            scrollRect.verticalNormalizedPosition = Mathf.Clamp01(pos);
        }

        /// <summary>
        /// Принудительно пересчитать layout (вызвать после изменения размера окна).
        /// </summary>
        public void ForceRecalculate()
        {
            lastText = "";
            if (inputField != null)
                OnTextChanged(inputField.text);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (inputField == null) inputField = GetComponent<InputField>();
            if (textComponent == null && inputField != null) textComponent = inputField.textComponent;
            if (scrollRect == null) scrollRect = GetComponent<ScrollRect>();
            if (contentRect == null && textComponent != null) contentRect = textComponent.rectTransform;
        }
#endif
    }
}
