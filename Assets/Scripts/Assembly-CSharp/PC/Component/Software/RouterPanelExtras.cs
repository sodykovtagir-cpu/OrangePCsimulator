using PC.Component.Networking;
using PC.Component.Software.OS;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
    // Binds the player's RouterPanel and reuses the player's WifiPanelPrefab.
    // LAN is driven by physical cable events, never by a virtual web button.
    public sealed class RouterPanelExtras : MonoBehaviour
    {
        [SerializeField] private TypoLinkWebsite website;
        [SerializeField] private RectTransform repeaterList;
        [SerializeField] private WifiNetworkEntry repeaterRowPrefab;
        [SerializeField] private Button scanButton;
        [SerializeField] private Button repeaterResetButton;
        [SerializeField] private Button apnResetButton;
        [SerializeField] private InputField ssidInput;
        [SerializeField] private InputField passwordInput;
        [SerializeField] private Text status;
        private WifiNetworkEntry selectedEntry;
        private int selectedId;

        private void Start()
        {
            if (website == null || repeaterList == null || repeaterRowPrefab == null) return;
            if (scanButton != null) scanButton.onClick.AddListener(Scan);
            if (repeaterResetButton != null) repeaterResetButton.onClick.AddListener(DisconnectSelected);
            if (apnResetButton != null) apnResetButton.onClick.AddListener(ResetForm);
            Scan();
        }

        private void OnDestroy()
        {
            if (scanButton != null) scanButton.onClick.RemoveListener(Scan);
            if (repeaterResetButton != null) repeaterResetButton.onClick.RemoveListener(DisconnectSelected);
            if (apnResetButton != null) apnResetButton.onClick.RemoveListener(ResetForm);
        }

        private void Scan()
        {
            if (repeaterList == null || website == null) return;
            selectedEntry = null;
            selectedId = 0;
            for (int i = repeaterList.childCount - 1; i >= 0; i--)
                Destroy(repeaterList.GetChild(i).gameObject);
            var current = website.CurrentRouter;
            var network = InternetService.Instance;
            if (current == null || network == null)
            { if (status != null) status.text = "Сначала подключи ПК к роутеру"; return; }
            // The PC is administering the MAIN router. This explicit context
            // resolves an otherwise ambiguous two-router provider direction.
            if (network.GatewayId == 0 && current.UpstreamId == 0)
                network.SetGateway(current.Id);
            int count = 0;
            foreach (var router in FindObjectsOfType<RouterDevice>())
            {
                if (router == null || router.Id == 0 || router == current || !router.IsPowered) continue;
                var row = Instantiate(repeaterRowPrefab, repeaterList);
                var rect = (RectTransform)row.transform;
                rect.localScale = Vector3.one;
                rect.sizeDelta = new Vector2(415f, 54f);
                var element = row.GetComponent<LayoutElement>();
                if (element == null) element = row.gameObject.AddComponent<LayoutElement>();
                element.preferredHeight = 54f;
                element.preferredWidth = 415f;
                row.InitializeRepeater(current, router, network,
                    entry => SelectEntry(entry, router.Id), () =>
                    {
                        row.Refresh();
                        if (status != null) status.text = "Повторитель #" + router.Id +
                            (router.UpstreamId == current.Id ? " подключён" : " отключён");
                    });
                count++;
            }
            if (status != null) status.text = "Роутер #" + current.Id +
                (count > 0 ? ". Найдено: " + count : "");
        }

        private void SelectEntry(WifiNetworkEntry entry, int id)
        {
            if (selectedEntry != null && selectedEntry != entry)
                selectedEntry.CancelPasswordEntry();
            selectedEntry = entry;
            selectedId = id;
        }

        private void DisconnectSelected()
        {
            var parent = website != null ? website.CurrentRouter : null;
            if (parent == null || selectedId == 0)
            { if (status != null) status.text = "Выбери повторитель в списке"; return; }
            var child = InternetService.Instance?.Router(selectedId);
            if (child == null || child.UpstreamId != parent.Id)
            { if (status != null) status.text = "Этот роутер не подключён к текущему"; return; }
            if (!child.UpstreamWireless)
            { if (status != null) status.text = "Подключён по LAN"; return; }
            child.DisconnectUpstream();
            if (selectedEntry != null) selectedEntry.CancelPasswordEntry();
            if (status != null) status.text = "Повторитель #" + selectedId + " отключён";
        }

        private void ResetForm()
        {
            if (ssidInput != null) ssidInput.text = "";
            if (passwordInput != null) passwordInput.text = "";
            if (status != null) status.text = "Несохранённые изменения сброшены";
        }
    }
}
