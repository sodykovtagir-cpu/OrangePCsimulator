using System;
using PC.Component.Networking;
using PC.Component.Cabling;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software.OS
{
    // Only scans on demand. No network polling or UI allocations in normal play.
    public sealed class WifiTaskbarController : MonoBehaviour
    {
        private OperatingSystem os;
        private WifiNetworkEntry rowPrefab;
        private RectTransform panel;
        private RectTransform list;
        private Text status;
        private Text powerLabel;
        private Image trayIcon;
        private static Font font;
        private static Font Font => font != null ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        private static readonly Color backdrop = new Color(.10f, .14f, .21f, .97f);
        private static readonly Color accent = new Color(.18f, .38f, .57f, 1f);
        private static readonly Color white = new Color(.95f, .96f, 1f);

        public void Initialize(OperatingSystem system, Transform taskbar, WifiNetworkEntry prefab, Sprite icon)
        {
            os = system;
            rowPrefab = prefab;
            if (taskbar == null || taskbar.parent == null || prefab == null) return;
            var tray = Box("Wi-Fi", taskbar, new Vector2(34, 34), new Vector2(-145, 0),
                new Vector2(1, .5f), new Vector2(1, .5f), accent);
            var trayButton = tray.gameObject.AddComponent<Button>();
            trayButton.onClick.AddListener(TogglePanel);
            var glyph = Box("Wi-Fi icon", tray, new Vector2(26, 26), Vector2.zero,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), Color.white);
            trayIcon = glyph.GetComponent<Image>();
            trayIcon.sprite = icon;
            trayIcon.preserveAspect = true;
            trayIcon.raycastTarget = false;
            if (icon == null) Label(tray, "Wi-Fi", new Vector2(34, 32), Vector2.zero, 12);

            panel = Box("Wi-Fi Panel", taskbar.parent, new Vector2(455, 340), new Vector2(-8, 44),
                new Vector2(1, 0), new Vector2(1, 0), backdrop);
            panel.pivot = new Vector2(1, 0);
            panel.anchoredPosition = new Vector2(-8, 44);
            Label(panel, "Wi-Fi", new Vector2(390, 34), new Vector2(0, -9), 23);
            var scan = ActionButton(panel, "Scan", new Vector2(98, 34), new Vector2(14, -49));
            scan.onClick.AddListener(Scan);
            var power = ActionButton(panel, "Wi-Fi: ON", new Vector2(196, 34), new Vector2(122, -49));
            powerLabel = power.GetComponentInChildren<Text>();
            power.onClick.AddListener(ToggleRadio);
            status = Label(panel, "", new Vector2(420, 30), new Vector2(0, -88), 14);
            var viewport = Box("Networks viewport", panel, new Vector2(429, 204), new Vector2(0, -123),
                new Vector2(.5f, 1), new Vector2(.5f, 1), new Color(0, 0, 0, .14f));
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            list = Box("Networks", viewport, new Vector2(415, 0), Vector2.zero,
                new Vector2(.5f, 1), new Vector2(.5f, 1), new Color(0, 0, 0, 0));
            list.pivot = new Vector2(.5f, 1);
            list.GetComponent<Image>().raycastTarget = false;
            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 4;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = list.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = list;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 24;
            panel.gameObject.SetActive(false);
            UpdateRadio();
        }

        private static RectTransform Box(string name, Transform parent, Vector2 size, Vector2 pos,
            Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin;
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;
            go.GetComponent<Image>().color = color;
            return rect;
        }

        private static Text Label(Transform parent, string value, Vector2 size, Vector2 pos, int fontSize)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;
            var text = go.GetComponent<Text>();
            text.font = Font;
            text.color = white;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleLeft;
            text.text = value;
            text.raycastTarget = false;
            return text;
        }

        private static Button ActionButton(Transform parent, string label, Vector2 size, Vector2 pos)
        {
            var rect = Box(label, parent, size, pos, new Vector2(0, 1), new Vector2(0, 1), accent);
            var button = rect.gameObject.AddComponent<Button>();
            var text = Label(rect, label, size, Vector2.zero, 16);
            var tr = (RectTransform)text.transform;
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.pivot = new Vector2(.5f, .5f);
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;
            text.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        private void TogglePanel()
        {
            if (panel == null) return;
            bool show = !panel.gameObject.activeSelf;
            panel.gameObject.SetActive(show);
            if (show) { panel.SetAsLastSibling(); Scan(); }
        }

        private void ToggleRadio()
        {
            var board = os != null ? os.Board : null;
            if (board == null) return;
            board.SetWifiEnabled(!board.WifiEnabled);
            UpdateRadio();
            Scan();
        }

        private void UpdateRadio()
        {
            var board = os != null ? os.Board : null;
            bool enabled = board != null && board.WifiEnabled;
            if (powerLabel != null) powerLabel.text = enabled ? "Wi-Fi: ON (2 W)" : "Wi-Fi: OFF (0 W)";
            if (trayIcon != null) trayIcon.color = enabled ? Color.white : new Color(.5f, .5f, .5f, 1f);
        }

        private void Scan()
        {
            if (list == null) return;
            for (int i = list.childCount - 1; i >= 0; i--) Destroy(list.GetChild(i).gameObject);
            UpdateRadio();
            var board = os != null ? os.Board : null;
            var service = InternetService.Instance;
            if (board == null || service == null || !board.Running)
            { status.text = "Сеть недоступна"; return; }
            int found = 0;
            if (board.WifiEnabled)
            {
                var routers = FindObjectsOfType<RouterDevice>();
                Array.Sort(routers, (a, b) =>
                    b.WifiSignal(board.transform, board.WifiRange)
                        .CompareTo(a.WifiSignal(board.transform, board.WifiRange)));
                foreach (var router in routers)
                {
                    if (router == null || router.Id == 0 || !router.IsPowered ||
                        router.WifiSignal(board.transform, board.WifiRange) <= 0f) continue;
                    var row = Instantiate(rowPrefab, list);
                    var rect = (RectTransform)row.transform;
                    rect.localScale = Vector3.one;
                    rect.sizeDelta = new Vector2(415, 54);
                    var element = row.GetComponent<LayoutElement>();
                    if (element == null) element = row.gameObject.AddComponent<LayoutElement>();
                    element.preferredHeight = 54;
                    element.preferredWidth = 415;
                    row.Initialize(board, router, service);
                    found++;
                }
            }
            // The cable-free game mode still needs a deliberate way to make a
            // logical LAN link. In physical mode no button can create a cable.
            if (!CableMode.Enabled)
            {
                foreach (var router in FindObjectsOfType<RouterDevice>())
                {
                    if (router == null || router.Id == 0 || !router.IsPowered ||
                        !router.WithinLanRange(board.transform)) continue;
                    var target = router;
                    var line = ActionButton(list, "LAN: " + target.Ssid +
                        (board.LanRouterId == target.Id ? " (Disconnect)" : " (Connect)"),
                        new Vector2(415, 42), Vector2.zero);
                    var element = line.gameObject.AddComponent<LayoutElement>();
                    element.preferredHeight = 42;
                    line.onClick.AddListener(() =>
                    {
                        if (board.LanRouterId == target.Id) board.ConfigureLan(0);
                        else if (!service.JoinLan(board, target.Id))
                        { status.text = "LAN: порт занят или роутер недоступен"; return; }
                        Scan();
                    });
                }
            }
            var connection = service.Status(board);
            status.text = board.WifiEnabled ?
                (found == 0 ? "Wi-Fi сети не найдены" : "Wi-Fi сетей: " + found) :
                "Wi-Fi выключен (экономия 2 Вт)";
            if (connection.Wired) status.text += "  |  LAN подключён";
        }
    }
}
