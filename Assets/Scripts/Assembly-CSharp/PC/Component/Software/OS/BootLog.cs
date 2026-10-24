using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software.OS
{
    /// <summary>Full-monitor startup diagnostics, independent of the PCOS/BIOS prefab animation.</summary>
    public sealed class BootLog : MonoBehaviour
    {
        private Text output;
        private bool verbose;
        private string lines = "";

        public static BootLog Create(ComputerSystem system, bool debug)
        {
            var root = system != null ? system.GetComponent<RectTransform>() : null;
            if (root == null) return null;
            var screen = new GameObject("Boot diagnostics", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            screen.transform.SetParent(root, false);
            screen.layer = root.gameObject.layer;
            var rect = (RectTransform)screen.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = screen.GetComponent<Image>();
            image.color = new Color(0.018f, 0.023f, 0.035f, 1f);
            image.raycastTarget = true;

            var label = new GameObject("Startup messages", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            label.transform.SetParent(rect, false);
            label.layer = root.gameObject.layer;
            var textRect = (RectTransform)label.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(24f, 24f);
            textRect.offsetMax = new Vector2(-24f, -24f);
            var text = label.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 20;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.color = new Color(0.85f, 0.9f, 0.92f, 1f);
            text.raycastTarget = false;

            var log = screen.AddComponent<BootLog>();
            log.output = text;
            log.verbose = debug;
            screen.SetActive(debug);
            return log;
        }

        public void Add(string message)
        {
            lines += message + "\n";
            if (output != null) output.text = lines;
        }

        public void Fail(string reason)
        {
            // Even without -debug the real cause must be visible, not just a BSOD.
            if (!verbose) lines = "";
            Add(reason);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Close()
        {
            Destroy(gameObject);
        }
    }
}
