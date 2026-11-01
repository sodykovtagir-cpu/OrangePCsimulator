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

        public void Initialize(Motherboard currentBoard, RouterDevice target, InternetService currentService)
        {
            board = currentBoard;
            router = target;
            service = currentService;
            enteringPassword = false;
            if (password != null) { password.contentType = InputField.ContentType.Password; password.text = ""; }
            if (connect != null) connect.onClick.AddListener(ConnectClicked);
            Refresh();
        }

        private bool Connected()
        {
            if (board == null || router == null || service == null || !board.WifiEnabled) return false;
            var status = service.Status(board);
            return !status.Wired && status.Router == router;
        }

        public void Refresh()
        {
            if (router == null || board == null) return;
            bool connected = Connected();
            if (connected && enteringPassword) enteringPassword = false;
            if (ssid != null) { ssid.text = router.Ssid; ssid.gameObject.SetActive(!enteringPassword); }
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

        private void ConnectClicked()
        {
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
