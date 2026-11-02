using System;
using PC.Component.Networking;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
    // Temporary self-contained UI. Later authored Website prefabs can call
    // InternetService/RouterDevice/Motherboard APIs without depending on this.
    public static class RouterWebPages
    {
        private static Font font;
        private static Font Font => font != null ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        private static readonly Color bg = new Color(.065f, .083f, .13f);
        private static readonly Color fg = new Color(.9f, .94f, 1f);
        private static readonly Color button = new Color(.17f, .32f, .57f);
        public static bool IsLocalAddress(string url) =>
            url == "192.168.1.1/panel" || url == "typolink.com/tariffs";

        private static GameObject Make(string name, Transform parent, bool image = false)
        {
            var o = new GameObject(name, typeof(RectTransform));
            o.transform.SetParent(parent, false);
            if (image) o.AddComponent<Image>();
            return o;
        }

        private static void Fill(RectTransform t)
        {
            t.anchorMin = Vector2.zero; t.anchorMax = Vector2.one;
            t.offsetMin = Vector2.zero; t.offsetMax = Vector2.zero;
        }

        private static GameObject Surface(Transform container, out Transform content)
        {
            var root = Make("Typo-Link page", container, true);
            Fill((RectTransform)root.transform);
            root.GetComponent<Image>().color = bg;
            var viewport = Make("Viewport", root.transform, true);
            Fill((RectTransform)viewport.transform);
            viewport.GetComponent<Image>().color = bg;
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            var body = Make("Content", viewport.transform);
            var rect = (RectTransform)body.transform;
            rect.anchorMin = new Vector2(0f, 1f); rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, 1f); rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            var layout = body.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 14, 14);
            layout.spacing = 8f;
            layout.childControlHeight = true; layout.childControlWidth = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            var fitter = body.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = root.AddComponent<ScrollRect>();
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = rect; scroll.horizontal = false; scroll.vertical = true;
            scroll.scrollSensitivity = 20f;
            content = body.transform;
            return root;
        }

        private static Text Label(Transform parent, string value, int size = 17)
        {
            var o = Make("Text", parent);
            var t = o.AddComponent<Text>(); t.font = Font; t.fontSize = size;
            t.color = fg; t.text = value;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            o.AddComponent<LayoutElement>().preferredHeight = size > 20 ? 42 : 35;
            return t;
        }

        private static Button AddButton(Transform parent, string label, Action run)
        {
            var o = Make(label, parent, true);
            o.GetComponent<Image>().color = button;
            o.AddComponent<LayoutElement>().preferredHeight = 42f;
            var b = o.AddComponent<Button>();
            b.onClick.AddListener(() => run());
            var child = Make("Label", o.transform);
            Fill((RectTransform)child.transform);
            var t = child.AddComponent<Text>();
            t.font = Font; t.fontSize = 17; t.color = fg;
            t.alignment = TextAnchor.MiddleCenter; t.text = label;
            return b;
        }

        private static InputField Entry(Transform parent, string placeholder, string value = "")
        {
            var o = Make("Input", parent, true);
            o.GetComponent<Image>().color = new Color(.17f, .19f, .25f);
            o.AddComponent<LayoutElement>().preferredHeight = 38f;
            var input = o.AddComponent<InputField>();
            var child = Make("Text", o.transform);
            var rect = (RectTransform)child.transform;
            Fill(rect); rect.offsetMin = new Vector2(8, 0); rect.offsetMax = new Vector2(-8, 0);
            var text = child.AddComponent<Text>(); text.font = Font;
            text.fontSize = 17; text.color = fg; text.supportRichText = false;
            text.alignment = TextAnchor.MiddleLeft;
            input.textComponent = text;
            input.text = value;
            var hint = Make("Placeholder", o.transform);
            Fill((RectTransform)hint.transform);
            var h = hint.AddComponent<Text>(); h.font = Font; h.fontSize = 17;
            h.fontStyle = FontStyle.Italic; h.color = new Color(.55f, .58f, .63f);
            h.alignment = TextAnchor.MiddleCenter; h.text = placeholder;
            input.placeholder = h;
            return input;
        }

        public static GameObject Message(Transform host, string text)
        {
            var root = Surface(host, out var body);
            Label(body, text, 21);
            return root;
        }

        public static GameObject Create(Browser browser, Transform host, Motherboard board, string url)
        {
            var root = Surface(host, out var body);
            var service = InternetService.Instance;
            if (service == null) { Label(body, "Сетевая служба не найдена"); return root; }
            Label(body, "TYPO-LINK  /  " + url, 23);
            if (url == "typolink.com/tariffs") Tariffs(browser, body, board, service);
            else Panel(browser, body, board, service);
            return root;
        }

        private static void Links(Browser browser, Transform body)
        {
            AddButton(body, "Тарифы", () => browser.ShowNetworkPage("typolink.com/tariffs"));
            AddButton(body, "Панель роутера", () => browser.ShowNetworkPage("192.168.1.1/panel"));
        }

        private static void Tariffs(Browser browser, Transform body, Motherboard board, InternetService service)
        {
            if (!service.CanOpenLocalPanel(board, out var router))
            { Label(body, "Сначала подключись к роутеру (Wi-Fi / LAN)."); Links(browser, body); return; }
            Label(body, "Текущий тариф: " + service.PlanName + " | потрачено: " +
                service.TotalMegabytes.ToString("0.0") + " МБ");
            Label(body, "Цена берётся за реально использованный трафик. 1 ГБ = 1024 МБ; " +
                "накопленные целые доллары списываются с баланса игры.");
            string[] notes = { "", "Медленно, сбои, часть сайтов недоступна.",
                "Умеренная скорость, редкие сбои.", "Быстро, почти без сбоев." };
            for (int i = 1; i <= 3; i++)
            {
                int id = i;
                AddButton(body, InternetService.PlanNames[i] + " — 1 ГБ / $" +
                    InternetService.PricePerGB[i] + ". " + notes[i],
                    () => { service.SetPlan(id); browser.ShowNetworkPage("typolink.com/tariffs"); });
            }
            Links(browser, body);
        }

        private static void Panel(Browser browser, Transform body, Motherboard board, InternetService service)
        {
            if (!service.CanOpenLocalPanel(board, out var router))
            { Label(body, "Панель доступна только с ПК, подключённого к роутеру."); Links(browser, body); return; }
            Label(body, "Роутер " + router.Id + " | сеть " + router.Ssid +
                " | шлюз " + (service.Gateway() == router ? "провайдера" : "повторитель"));
            Label(body, "SSID (имя точки Wi-Fi)");
            var ssid = Entry(body, "Новое имя сети", router.Ssid);
            AddButton(body, "Сохранить имя Wi-Fi", () =>
            { if (router.SetSsid(ssid.text)) browser.ShowNetworkPage("192.168.1.1/panel");
              else Label(body, "Имя сети должно быть от 1 до 32 символов"); });
            Label(body, "Новый пароль Wi-Fi (8–63 символа)");
            var key = Entry(body, "Новый пароль"); key.contentType = InputField.ContentType.Password;
            AddButton(body, "Сохранить пароль", () =>
            { if (router.SetPassword(key.text)) browser.ShowNetworkPage("192.168.1.1/panel");
              else Label(body, "Нужен пароль от 8 до 63 символов"); });
            AddButton(body, "Назначить шлюзом провайдера", () =>
            { if (service.SetGateway(router.Id)) browser.ShowNetworkPage("192.168.1.1/panel"); });
            Label(body, "Другие роутеры (подключить к этому как повторители):");
            foreach (var other in UnityEngine.Object.FindObjectsOfType<RouterDevice>())
            {
                if (other == null || other == router || other.Id == 0) continue;
                var child = other;
                Label(body, "#" + child.Id + ": " + child.Ssid +
                    " / " + (child.UpstreamId == 0 ? "нет родителя" : "родитель #" + child.UpstreamId));
                // The code-built fallback is used only without RouterPanel.prefab.
                // In cable mode LAN is automatic; never offer a fake LAN button.
                if (!PC.Component.Cabling.CableMode.Enabled)
                    AddButton(body, "Подключить #" + child.Id + " через LAN", () =>
                    { if (child.ConnectUpstream(router.Id, false)) browser.ShowNetworkPage("192.168.1.1/panel");
                      else Label(body, "LAN: слишком далеко, нет порта или получился цикл"); });
                Label(body, "Пароль основной Wi-Fi сети для #" + child.Id);
                var parentKey = Entry(body, "Введи пароль");
                parentKey.contentType = InputField.ContentType.Password;
                AddButton(body, "Подключить #" + child.Id + " как Wi-Fi повторитель", () =>
                { if (!string.IsNullOrEmpty(parentKey.text) &&
                      child.ConnectUpstream(router.Id, true, parentKey.text))
                      browser.ShowNetworkPage("192.168.1.1/panel");
                  else Label(body, "Wi-Fi: неверный пароль, нет сигнала или возник цикл"); });
            }
            if (router.UpstreamId != 0)
                AddButton(body, "Отключить этот роутер от родителя", () =>
                { router.DisconnectUpstream(); browser.ShowNetworkPage("192.168.1.1/panel"); });
            Links(browser, body);
        }
    }
}
