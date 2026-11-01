using PC.Component.Networking;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
    // Uses the authored RouterPanel: its Scan/Reset buttons and empty repeater scroll list.
    public sealed class RouterPanelExtras : MonoBehaviour
    {
        [SerializeField] private TypoLinkWebsite website;
        [SerializeField] private RectTransform repeaterList;
        [SerializeField] private Button scanButton;
        [SerializeField] private Button repeaterResetButton;
        [SerializeField] private Button apnResetButton;
        [SerializeField] private InputField ssidInput;
        [SerializeField] private InputField passwordInput;
        [SerializeField] private Text status;
        private InputField targetId;
        private Transform rows;
        private static Font font;
        private static Font Font => font != null ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        private void Start()
        {
            if (website == null || repeaterList == null) return;
            targetId = Field(repeaterList, "ID повторителя");
            website.SetRepeaterInput(targetId);
            rows = new GameObject("Доступные роутеры", typeof(RectTransform)).transform;
            rows.SetParent(repeaterList, false);
            // Items live inside the existing Scroll View, never a second independent page.
            var layout = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = false;
            layout.spacing = 3;
            rows.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var element = rows.gameObject.AddComponent<LayoutElement>();
            element.flexibleWidth = 1;
            if (scanButton != null) scanButton.onClick.AddListener(Scan);
            if (repeaterResetButton != null) repeaterResetButton.onClick.AddListener(DisconnectSelected);
            if (apnResetButton != null) apnResetButton.onClick.AddListener(ResetForm);
            Scan();
        }

        private static RectTransform Make(Transform parent, string name, float height, bool image)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(0, height);
            if (image) go.AddComponent<Image>().color = new Color(.2f, .3f, .42f, 1f);
            var size = go.AddComponent<LayoutElement>();
            size.preferredHeight = height;
            size.flexibleWidth = 1;
            return rect;
        }

        private static Text Label(Transform parent, string value, float width)
        {
            var go = new GameObject(value, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            var element = go.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.flexibleWidth = width < 0 ? 1 : 0;
            var text = go.GetComponent<Text>();
            text.font = Font;
            text.color = Color.white;
            text.fontSize = 14;
            text.alignment = TextAnchor.MiddleLeft;
            text.text = value;
            text.raycastTarget = false;
            return text;
        }

        private static InputField Field(Transform parent, string hint)
        {
            var row = Make(parent, hint, 32, true);
            var input = row.gameObject.AddComponent<InputField>();
            input.contentType = InputField.ContentType.IntegerNumber;
            var text = Label(row, "", -1);
            var textRect = (RectTransform)text.transform;
            textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(6, 0); textRect.offsetMax = new Vector2(-6, 0);
            input.textComponent = text;
            var placeholder = Label(row, hint, -1);
            var placeholderRect = (RectTransform)placeholder.transform;
            placeholderRect.anchorMin = Vector2.zero; placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = new Vector2(6, 0); placeholderRect.offsetMax = new Vector2(-6, 0);
            placeholder.color = new Color(.7f, .7f, .7f);
            input.placeholder = placeholder;
            return input;
        }

        private static Button Action(Transform row, string title, float width, UnityEngine.Events.UnityAction callback)
        {
            var go = new GameObject(title, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(row, false);
            go.GetComponent<Image>().color = new Color(.22f, .44f, .62f);
            var el = go.AddComponent<LayoutElement>(); el.preferredWidth = width; el.preferredHeight = 34;
            var button = go.GetComponent<Button>(); button.onClick.AddListener(callback);
            var text = Label(go.transform, title, width);
            var rect = (RectTransform)text.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            text.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        private void Scan()
        {
            if (rows == null) return;
            for (int i = rows.childCount - 1; i >= 0; --i) Destroy(rows.GetChild(i).gameObject);
            var current = website.CurrentRouter;
            if (current == null) { if (status != null) status.text = "Сначала подключи ПК к роутеру"; return; }
            int count = 0;
            foreach (var router in FindObjectsOfType<RouterDevice>())
            {
                if (router == null || router.Id == 0 || router == current || !router.IsPowered) continue;
                var target = router;
                var line = Make(rows, router.Ssid, 38, false);
                var layout = line.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 3;
                layout.childForceExpandWidth = false;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                string state = router.UpstreamId == current.Id ? " (повторитель)" : "";
                Label(line, "#" + router.Id + " " + router.Ssid + state, -1);
                Action(line, "Wi-Fi", 60, () => Connect(target, true));
                Action(line, "LAN", 54, () => Connect(target, false));
                count++;
            }
            if (count == 0)
            {
                var empty = Make(rows, "Пусто", 32, false);
                var text = Label(empty, "Нет включённых роутеров для подключения", -1);
                var rect = (RectTransform)text.transform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
            if (status != null) status.text = "Роутер #" + current.Id + ". Найдено: " + count;
        }

        private void Connect(RouterDevice target, bool wifi)
        {
            if (target == null || website == null || targetId == null) return;
            targetId.text = target.Id.ToString();
            if (wifi) website.AttachRepeaterWifi(); else website.AttachRepeaterLan();
            if (target.UpstreamId == website.CurrentRouter?.Id && target.UpstreamWireless == wifi)
            {
                Scan();
                if (status != null) status.text = "#" + target.Id + " подключён по " + (wifi ? "Wi-Fi" : "LAN");
            }
            // On failure the existing TypoLinkWebsite shows a detailed error.
        }

        private void DisconnectSelected()
        {
            var parent = website != null ? website.CurrentRouter : null;
            if (parent == null || targetId == null || !int.TryParse(targetId.text, out var id))
            { if (status != null) status.text = "Укажи ID повторителя"; return; }
            var child = InternetService.Instance?.Router(id);
            if (child == null || child.UpstreamId != parent.Id)
            { if (status != null) status.text = "Этот роутер не подключён к текущему"; return; }
            child.DisconnectUpstream();
            Scan();
            if (status != null) status.text = "Повторитель #" + id + " отключён";
        }

        private void ResetForm()
        {
            if (ssidInput != null) ssidInput.text = "";
            if (passwordInput != null) passwordInput.text = "";
            if (status != null) status.text = "Несохранённые изменения сброшены";
        }
    }
}
