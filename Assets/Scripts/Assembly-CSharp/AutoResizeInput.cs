using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
    /// <summary>
    /// InputField который автоматически подстраивает высоту под содержимое.
    /// Вешается на GameObject с InputField + Text (textComponent).
    /// </summary>
    public class AutoResizeInput : MonoBehaviour
    {
        [SerializeField] private InputField inputField;
        [SerializeField] private Text textComponent;
        [SerializeField] private float minHeight = 24f;
        [SerializeField] private float maxHeight = 400f;
        [SerializeField] private float padding = 8f;
        [SerializeField] private float lineHeight = 18f;

        private RectTransform rectTransform;
        private ContentSizeFitter fitter;
        private string lastText = "";
        private int lastCaretPos = 0;

        void Awake()
        {
            if (inputField == null) inputField = GetComponent<InputField>();
            if (textComponent == null && inputField != null) textComponent = inputField.textComponent;
            rectTransform = GetComponent<RectTransform>();
        }

        void OnEnable()
        {
            if (inputField != null)
            {
                inputField.onValueChanged.AddListener(OnTextChanged);
                // Подписываемся на выделение/каретку
            }
            ResizeToFit();
        }

        void OnDisable()
        {
            if (inputField != null)
                inputField.onValueChanged.RemoveListener(OnTextChanged);
        }

        void OnTextChanged(string newText)
        {
            if (newText == lastText) return;

            // Сохраняем позицию каретки
            if (inputField != null)
                lastCaretPos = inputField.caretPosition;

            lastText = newText;
            ResizeToFit();

            // Восстанавливаем каретку после пересчёта
            if (inputField != null)
            {
                inputField.caretPosition = lastCaretPos;
                inputField.selectionAnchorPosition = lastCaretPos;
                inputField.selectionFocusPosition = lastCaretPos;
            }
        }

        void ResizeToFit()
        {
            if (textComponent == null || rectTransform == null) return;

            // Считаем количество строк
            string text = textComponent.text ?? "";
            int lineCount = CountLines(text);

            // Вычисляем высоту
            float contentHeight = lineHeight * lineCount + padding * 2;
            float newHeight = Mathf.Clamp(contentHeight, minHeight, maxHeight);

            // Обновляем размер
            var size = rectTransform.sizeDelta;
            if (Mathf.Abs(size.y - newHeight) > 1f)
            {
                rectTransform.sizeDelta = new Vector2(size.x, newHeight);
            }
        }

        int CountLines(string text)
        {
            if (string.IsNullOrEmpty(text)) return 1;

            int lines = 1;
            float width = rectTransform != null ? rectTransform.rect.width - padding * 2 : 200f;

            // Считаем явные переносы
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n') lines++;
            }

            // Оцениваем переносы по ширине (грубая оценка)
            float charWidth = textComponent.fontSize * 0.6f;
            float charsPerLine = width / charWidth;
            if (charsPerLine <= 0) charsPerLine = 1;

            // Разбиваем по \n и считаем переносы в каждой строке
            string[] explicitLines = text.Split('\n');
            int totalLines = 0;
            for (int i = 0; i < explicitLines.Length; i++)
            {
                string line = explicitLines[i];
                // Считаем символы (приблизительно)
                float lineWidth = 0;
                int wrappedLines = 1;
                for (int j = 0; j < line.Length; j++)
                {
                    char c = line[j];
                    float cw = c == ' ' ? charWidth * 0.5f : charWidth;
                    lineWidth += cw;
                    if (lineWidth > width)
                    {
                        wrappedLines++;
                        lineWidth = cw;
                    }
                }
                totalLines += wrappedLines;
            }

            return totalLines > 0 ? totalLines : 1;
        }

        /// <summary>
        /// Вызвать вручную если размер изменился извне.
        /// </summary>
        public void ForceResize()
        {
            lastText = "";
            if (textComponent != null)
                OnTextChanged(textComponent.text);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (inputField == null) inputField = GetComponent<InputField>();
            if (textComponent == null && inputField != null) textComponent = inputField.textComponent;
        }
#endif
    }
}
