using System;
using PC.Component.Networking;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software.OS
{
    // Binds the player's WifiPanelPrefab: one row for each visible router.
    public sealed class WifiNetworkEntry : MonoBehaviour
    {
        [SerializeField] private Text ssid;
        [SerializeField] private Text description;
        [SerializeField] private GameObject icon;
        [SerializeField] private GameObject passwordPanel;
        [SerializeField] private InputField password;
        [SerializeField] private Text passwordMessage;
        [SerializeField] private Button connect;
        [SerializeField] private Text connectLabel;

        private Motherboard board;
        private RouterDevice router;
        private InternetService service;
        private bool enteringPassword;
        private Action<WifiNetworkEntry> onSelected;
        private Action onRepeaterChanged;
        private RouterDevice repeaterParent;
        private RouterDevice repeaterTarget;

        public void Initialize(Motherboard currentBoard, RouterDevice target, InternetService currentService,
            Action<WifiNetworkEntry> selected = null)
        {
            board = currentBoard;
            router = target;
            service = currentService;
            enteringPassword = false;
            onSelected = selected;
            repeaterParent = repeaterTarget = null;
            if (password != null) { password.contentType = InputField.ContentType.Password; password.text = ""; }
            if (connect != null) connect.onClick.AddListener(ConnectClicked);
            Refresh();
        }

        // Used on the main router's web panel: each card is a candidate
        // repeater, and the typed key belongs to the MAIN Wi-Fi network.
        public void InitializeRepeater(RouterDevice parent, RouterDevice target,
            InternetService network, Action<WifiNetworkEntry> selected, Action changed)
        {
            board = null;
            router = null;
            service = network;
            repeaterParent = parent;
            repeaterTarget = target;
            onSelected = selected;
            onRepeaterChanged = changed;
            enteringPassword = false;
            if (password != null)
            {
                password.contentType = InputField.ContentType.Password;
                password.text = "";
            }
            if (connect != null)
            {
                connect.onClick.RemoveAllListeners();
                connect.onClick.AddListener(ConnectClicked);
            }
            Refresh();
        }

        // PCOS creates its own instances of the shared prefab. Do not recolor
        // the asset: the router website still uses its authored text palette.
        // Button captions and input/placeholder text keep their contrast.
        public void SetPcosCardColors()
        {
            if (ssid != null) ssid.color = Color.white;
            if (description != null) description.color = Color.white;
            if (passwordMessage != null) passwordMessage.color = Color.white;
            // The author's icon sprite stays intact; tint only this PCOS row.
            if (icon != null && icon.TryGetComponent<Image>(out var wifiIcon))
                wifiIcon.color = Color.white;
        }

        public void CancelPasswordEntry()
        {
            enteringPassword = false;
            if (password != null) password.text = "";
            if (passwordPanel != null) passwordPanel.SetActive(false);
            Refresh();
        }

        private bool Connected()
        {
            if (board == null || router == null || service == null) return false;
            var status = service.Status(board);
            return board.WifiEnabled && !status.Wired && status.Router == router;
        }

        public void Refresh()
        {
            if (repeaterTarget != null)
            {
                bool repeaterConnected = repeaterParent != null &&
                    repeaterTarget.UpstreamId == repeaterParent.Id &&
                    (repeaterTarget.UpstreamWireless || PC.Component.Cabling.CableMode.Enabled);
                bool wired = repeaterConnected && !repeaterTarget.UpstreamWireless;
                if (ssid != null)
                {
                    ssid.text = "#" + repeaterTarget.Id + " " + repeaterTarget.Ssid;
                    ssid.gameObject.SetActive(!enteringPassword);
                }
                if (icon != null) icon.SetActive(!enteringPassword);
                if (description != null)
                {
                    description.text = wired ? "Подключён по LAN" :
                        repeaterConnected ? "Повторитель по Wi-Fi" : "Не подключён";
                    description.gameObject.SetActive(!enteringPassword);
                }
                if (connectLabel != null)
                    connectLabel.text = wired ? "LAN" : repeaterConnected ? "Disconnect" : "Connect";
                if (connect != null) connect.interactable = !wired;
                if (passwordPanel != null) passwordPanel.SetActive(enteringPassword && !repeaterConnected);
                return;
            }
            if (router == null || board == null) return;
            if (connect != null) connect.interactable = true;
            bool connected = Connected();
            if (connected && enteringPassword) enteringPassword = false;
            if (ssid != null) { ssid.text = router.Ssid;
                ssid.gameObject.SetActive(!enteringPassword); }
            if (icon != null) icon.SetActive(!enteringPassword);
            if (description != null)
            {
                description.text = connected ? "Connected" :
                    "Signal: " + Mathf.RoundToInt(router.WifiSignal(board.transform, board.WifiRange) * 100f) + "%";
                description.gameObject.SetActive(!enteringPassword);
            }
            if (passwordPanel != null) passwordPanel.SetActive(enteringPassword);
            if (connectLabel != null) connectLabel.text = connected ? "Disconnect" : "Connect";
        }

        private void ConnectRepeaterClicked()
        {
            if (service == null || repeaterParent == null || repeaterTarget == null) return;
            if (repeaterTarget.UpstreamId == repeaterParent.Id &&
                (repeaterTarget.UpstreamWireless || PC.Component.Cabling.CableMode.Enabled))
            {
                if (!repeaterTarget.UpstreamWireless) return; // unplug physical LAN
                repeaterTarget.DisconnectUpstream();
                CancelPasswordEntry();
                onRepeaterChanged?.Invoke();
                return;
            }
            if (!enteringPassword)
            {
                onSelected?.Invoke(this);
                enteringPassword = true;
                if (password != null) password.text = "";
                if (passwordMessage != null) passwordMessage.text = "Пароль основной сети:";
                Refresh();
                if (password != null) password.ActivateInputField();
                return;
            }
            string key = password != null ? password.text : "";
            // An empty or wrong key is not replaced with parent.Password.
            if (string.IsNullOrEmpty(key) ||
                !repeaterParent.CredentialsMatch(repeaterParent.Ssid, key))
            {
                if (passwordMessage != null) passwordMessage.text = "Неверный пароль основной сети";
                return;
            }
            if (!repeaterTarget.ConnectUpstream(repeaterParent.Id, true, key))
            {
                if (passwordMessage != null)
                    passwordMessage.text = "Нет сигнала, питания или возник цикл";
                return;
            }
            CancelPasswordEntry();
            onRepeaterChanged?.Invoke();
        }

        private void ConnectClicked()
        {
            if (repeaterTarget != null) { ConnectRepeaterClicked(); return; }
            if (board == null || router == null || service == null) return;
            if (Connected())
            {
                board.ConfigureLan(0); // does not unplug an actual LAN cable
                enteringPassword = false;
                Refresh();
                return;
            }
            if (!board.WifiEnabled) { if (passwordMessage != null) passwordMessage.text = "Wi-Fi выключен"; return; }
            if (!enteringPassword)
            {
                onSelected?.Invoke(this);
                enteringPassword = true;
                if (password != null) password.text = "";
                if (passwordMessage != null) passwordMessage.text = "Enter Password:";
                Refresh();
                if (password != null) password.ActivateInputField();
                return;
            }
            // Check THIS router's credentials, not just any AP sharing an SSID.
            if (password == null || !router.CredentialsMatch(router.Ssid, password.text))
            {
                if (passwordMessage != null) passwordMessage.text = "Неверный пароль";
                return;
            }
            if (!service.JoinWifi(board, router.Ssid, password.text))
            {
                if (passwordMessage != null) passwordMessage.text = "Сеть недоступна, нажми Scan";
                return;
            }
            enteringPassword = false;
            Refresh();
            if (!Connected() && description != null)
                description.text = "Wi-Fi сохранён; проводной LAN имеет приоритет";
        }
    }
}
