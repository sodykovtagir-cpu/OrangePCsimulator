using System;
using PC.Component.Networking;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software.OS
{
    // Presentation lives in PCOS.prefab; this component only binds its controls.
    // Scan allocates rows only on request, not during ordinary gameplay.
    public sealed class WifiTaskbarController : MonoBehaviour
    {
        private OperatingSystem os;
        private WifiNetworkEntry rowPrefab;
        private Button trayButton;
        private RectTransform panel;
        private Button scanButton;
        private Button powerButton;
        private RectTransform list;
        private Text status;
        private Text powerLabel;
        private Image trayIcon;
        private WifiNetworkEntry selectedEntry;

        public void Initialize(OperatingSystem system, WifiNetworkEntry prefab, Button tray,
            RectTransform menu, Button scan, Button power, RectTransform rows,
            Text message, Text powerText, Image icon)
        {
            os = system;
            rowPrefab = prefab;
            trayButton = tray;
            panel = menu;
            scanButton = scan;
            powerButton = power;
            list = rows;
            status = message;
            powerLabel = powerText;
            trayIcon = icon;
            if (rowPrefab == null || trayButton == null || panel == null ||
                scanButton == null || powerButton == null || list == null) return;
            trayButton.onClick.AddListener(TogglePanel);
            scanButton.onClick.AddListener(Scan);
            powerButton.onClick.AddListener(ToggleRadio);
            panel.gameObject.SetActive(false);
            UpdateRadio();
        }

        private void OnDestroy()
        {
            if (trayButton != null) trayButton.onClick.RemoveListener(TogglePanel);
            if (scanButton != null) scanButton.onClick.RemoveListener(Scan);
            if (powerButton != null) powerButton.onClick.RemoveListener(ToggleRadio);
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
            Scan();
        }

        private void UpdateRadio()
        {
            var board = os != null ? os.Board : null;
            bool enabled = board != null && board.WifiEnabled;
            if (powerLabel != null) powerLabel.text = enabled ? "Wi-Fi: ON" : "Wi-Fi: OFF";
            // Preserve the icon sprite and its authored colors; only reduce opacity.
            if (trayIcon != null)
            {
                var color = trayIcon.color;
                color.a = enabled ? 1f : .45f;
                trayIcon.color = color;
            }
        }

        private void Scan()
        {
            if (list == null || status == null) return;
            selectedEntry = null;
            for (int i = list.childCount - 1; i >= 0; i--)
                Destroy(list.GetChild(i).gameObject);
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
                    AddRow(board, router, service);
                    found++;
                }
            }
            var connection = service.Status(board);
            status.text = board.WifiEnabled ?
                (found == 0 ? "Wi-Fi сети не найдены" : "Wi-Fi сетей: " + found) :
                "Wi-Fi выключен";
            if (connection.Wired) status.text += "  |  LAN подключён";
        }

        private void SelectEntry(WifiNetworkEntry entry)
        {
            if (selectedEntry != null && selectedEntry != entry)
                selectedEntry.CancelPasswordEntry();
            selectedEntry = entry;
        }

        private void AddRow(Motherboard board, RouterDevice router, InternetService service)
        {
            var row = Instantiate(rowPrefab, list);
            row.SetPcosCardColors();
            var rect = (RectTransform)row.transform;
            rect.localScale = Vector3.one;
            rect.sizeDelta = new Vector2(415, 54);
            var element = row.GetComponent<LayoutElement>();
            if (element == null) element = row.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 54;
            element.preferredWidth = 415;
            // This prefab is shared with the router site. Remove its root
            // background only from the PCOS instance, not the authored asset.
            var background = row.GetComponent<Image>();
            if (background != null) Destroy(background);
            row.Initialize(board, router, service, SelectEntry);
        }
    }
}
