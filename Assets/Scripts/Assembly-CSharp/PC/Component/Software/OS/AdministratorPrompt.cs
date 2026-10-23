using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PC.Component.Software.OS
{
    /// <summary>A modal PCOS password challenge. The typed password never enters
    /// a Lua VM, an app argument, PlayerPrefs, or a saved file.</summary>
    public sealed class AdministratorPrompt : MonoBehaviour
    {
        private Func<string, bool> verify;
        private Action<bool> complete;
        private InputField input;
        private Text status;
        private bool finished;
        private Font font;

        public static AdministratorPrompt Show(Transform parent, Func<string, bool> verify,
            Action<bool> complete, bool russian)
        {
            if (parent == null) { complete?.Invoke(false); return null; }
            var root = new GameObject("Administrator confirmation", typeof(RectTransform),
                typeof(Image), typeof(AdministratorPrompt));
            root.transform.SetParent(parent, false);
            root.transform.SetAsLastSibling();
            var rt = (RectTransform)root.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            root.GetComponent<Image>().color = new Color(0f, 0f, 0f, .55f);
            var prompt = root.GetComponent<AdministratorPrompt>();
            prompt.verify = verify;
            prompt.complete = complete;
            prompt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            prompt.Build(russian);
            return prompt;
        }

        private static RectTransform Rect(Transform parent, string name, float x, float y,
            float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        private Text Label(Transform parent, string text, float x, float y, float w, float h)
        {
            var rt = Rect(parent, "Text", x, y, w, h);
            var label = rt.gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = 15;
            label.color = new Color(.14f, .15f, .17f);
            label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false;
            label.text = text;
            return label;
        }

        private void Button(Transform parent, string name, string text, float x, float y,
            float w, Action click)
        {
            var rt = Rect(parent, name, x, y, w, 34);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(.82f, .84f, .86f);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            button.onClick.AddListener(() => click());
            var label = Label(rt, text, 0, 0, w, 34);
            label.alignment = TextAnchor.MiddleCenter;
        }

        private void Build(bool russian)
        {
            var panel = Rect(transform, "Panel", 0, 0, 380, 188);
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, .5f);
            panel.pivot = new Vector2(.5f, .5f);
            panel.anchoredPosition = Vector2.zero;
            var image = panel.gameObject.AddComponent<Image>();
            image.color = new Color(.96f, .97f, .98f);
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.31f, .32f, .34f);
            outline.effectDistance = new Vector2(1, -1);
            Label(panel, russian ? "Подтверждение администратора" : "Administrator confirmation",
                18, 12, 344, 30);
            Label(panel, russian ? "Пароль системного диска:" : "System drive password:",
                18, 52, 344, 25);

            var field = Rect(panel, "Password", 18, 80, 344, 34);
            var bg = field.gameObject.AddComponent<Image>();
            bg.color = Color.white;
            input = field.gameObject.AddComponent<InputField>();
            input.targetGraphic = bg;
            var inputText = Label(field, "", 8, 2, 328, 30);
            inputText.supportRichText = false;
            input.textComponent = inputText;
            input.contentType = InputField.ContentType.Password;
            input.lineType = InputField.LineType.SingleLine;
            input.onEndEdit.AddListener(value =>
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Confirm();
            });
            status = Label(panel, "", 18, 116, 344, 24);
            status.color = new Color(.65f, .18f, .18f);
            Button(panel, "Cancel", russian ? "Отмена" : "Cancel", 18, 148, 158,
                () => Finish(false));
            Button(panel, "Confirm", russian ? "Подтвердить" : "Confirm", 204, 148, 158,
                Confirm);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(field.gameObject);
            input.ActivateInputField();
        }

        private void Confirm()
        {
            if (finished) return;
            if (verify != null && verify(input != null ? input.text : ""))
            {
                Finish(true);
                return;
            }
            if (input != null) { input.text = ""; input.ActivateInputField(); }
            if (status != null) status.text = "Incorrect password / Неверный пароль";
        }

        public void Cancel() { Finish(false); }

        private void Finish(bool allowed)
        {
            if (finished) return;
            finished = true;
            var callback = complete;
            complete = null;
            Destroy(gameObject);
            callback?.Invoke(allowed);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) Finish(false);
        }

        private void OnDestroy()
        {
            if (finished) return;
            finished = true;
            var callback = complete;
            complete = null;
            callback?.Invoke(false);
        }
    }
}
