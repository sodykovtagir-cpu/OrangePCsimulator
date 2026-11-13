using System;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software.Sites
{
    public static class SiteWidgets
    {
        public static Font Font(string name = "default")
        {
            var font = Resources.Load<Font>("SiteFonts/" + name);
            return font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        public static GameObject Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
            go.transform.SetParent(parent, false); return go;
        }
        public static void Fill(RectTransform rect, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top);
        }
        public static RectTransform Box(GameObject go, float x, float y, float w, float h)
        {
            var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h); return rect;
        }
        public static GameObject Panel(string name, Transform parent, Color color)
        {
            var go = Node(name, parent); Fill((RectTransform)go.transform); go.AddComponent<Image>().color = color; return go;
        }
        public static Text Label(Transform parent, string text, int size = 14)
        {
            var go = Node("Label", parent); var label = go.AddComponent<Text>();
            label.font = Font(); label.text = text; label.fontSize = size; label.color = Color.black;
            label.supportRichText = false; label.raycastTarget = false; label.alignment = TextAnchor.MiddleLeft;
            Fill((RectTransform)go.transform); return label;
        }
        public static Button Button(Transform parent, string title, Action action)
        {
            var go = Panel(title, parent, new Color(.83f, .83f, .83f)); var button = go.AddComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>(); button.onClick.AddListener(() => action());
            Label(go.transform, title).alignment = TextAnchor.MiddleCenter; return button;
        }
        public static InputField Input(Transform parent, string value, Action<string> changed, bool multiline = false)
        {
            var go = Panel("Input", parent, Color.white); var input = go.AddComponent<InputField>();
            var label = Label(go.transform, "", 14); Fill(label.rectTransform, 5, 2, 5, 2);
            label.alignment = multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            input.textComponent = label; input.targetGraphic = go.GetComponent<Image>();
            input.lineType = multiline ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
            input.characterLimit = multiline ? SiteDocument.MaxScriptLength : 4096; input.text = value ?? "";
            input.onEndEdit.AddListener(v => changed(v)); return input;
        }
        public static RectTransform Scroll(Transform parent)
        {
            var go = Panel("Scroll", parent, new Color(.74f, .74f, .74f));
            var viewport = Panel("Viewport", go.transform, Color.white); viewport.AddComponent<Mask>().showMaskGraphic = false;
            var body = Node("Content", viewport.transform); var rect = (RectTransform)body.transform;
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(0, 1000); rect.anchoredPosition = Vector2.zero;
            var scroll = go.AddComponent<ScrollRect>(); scroll.viewport = (RectTransform)viewport.transform;
            scroll.content = rect; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            return rect;
        }
        public static Color ColorValue(string value)
        {
            Color color; return ColorUtility.TryParseHtmlString(value, out color) ? color : Color.white;
        }
        public static void Clear(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            { var child = parent.GetChild(i).gameObject; child.SetActive(false); UnityEngine.Object.Destroy(child); }
        }
    }
}
