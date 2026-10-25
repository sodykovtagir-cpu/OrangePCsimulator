using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software.OS
{
    /// <summary>Non-blocking diagnostics over the existing PCOS loading screens.</summary>
    public sealed class BootLog : MonoBehaviour
    {
        private const int MaxLines = 12;
        private readonly Queue<string> lines = new Queue<string>();
        private Text output;
        private bool verbose;

        public static BootLog Create(ComputerSystem system, bool debug)
        {
            var root = system != null ? system.GetComponent<RectTransform>() : null;
            if (root == null) return null;

            // Cover the monitor for layout purposes, but do not paint over PCOS
            // or intercept the account/password UI underneath the overlay.
            var screen = new GameObject("Boot diagnostics", typeof(RectTransform));
            screen.transform.SetParent(root, false);
            screen.layer = root.gameObject.layer;
            var rect = (RectTransform)screen.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsLastSibling();

            // A compact upper-left panel stays clear of the centered logo,
            // account name, loading spinner and optional password form.
            var panel = new GameObject("Startup log panel", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(rect, false);
            panel.layer = root.gameObject.layer;
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = new Vector2(0.02f, 0.55f);
            panelRect.anchorMax = new Vector2(0.37f, 0.97f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var background = panel.GetComponent<Image>();
            background.color = new Color(0.015f, 0.02f, 0.03f, 0.68f);
            background.raycastTarget = false;

            var label = new GameObject("Startup messages", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Text));
            label.transform.SetParent(panelRect, false);
            label.layer = root.gameObject.layer;
            var textRect = (RectTransform)label.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 8f);
            textRect.offsetMax = new Vector2(-10f, -8f);
            var text = label.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 14;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.color = new Color(0.85f, 0.92f, 0.96f, 1f);
            text.raycastTarget = false;

            var log = screen.AddComponent<BootLog>();
            log.output = text;
            log.verbose = debug;
            screen.SetActive(debug);
            return log;
        }

        public void Stage(string title)
        {
            lines.Clear();
            Add("=== " + title + " ===");
        }

        public void Add(string message)
        {
            foreach (var line in (message ?? "").Split('\n'))
            {
                lines.Enqueue(line);
                while (lines.Count > MaxLines) lines.Dequeue();
            }
            if (output != null) output.text = string.Join("\n", lines.ToArray());
        }

        public void Fail(string reason)
        {
            // Without -debug, show only the reason; with it, retain the stage.
            if (!verbose) lines.Clear();
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
