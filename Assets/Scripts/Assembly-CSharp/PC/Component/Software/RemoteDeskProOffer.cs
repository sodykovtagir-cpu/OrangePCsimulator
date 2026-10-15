using PC.Component.Software.OS;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
    /// <summary>The RemoteDesk upgrade is a genuine, independently closable PCOS window.</summary>
    public class RemoteDeskProOffer : App
    {
        [SerializeField] private Sprite banner;
        private Text feedback;
        private bool built;

        protected override bool ShowMenuBar => false;

        public override void Open(string content)
        {
            base.Open(content);
            if (system == null) return;
            if (RemoteDesk.HasProLicense(system))
            {
                Close();
                return;
            }
            var window = transform as RectTransform;
            var host = transform.parent as RectTransform;
            if (window != null && host != null)
            {
                float inset = system.GetTaskbarInsetLocalHeight();
                window.sizeDelta = new Vector2(Mathf.Min(520f, Mathf.Max(270f, host.rect.width - 16f)),
                    Mathf.Min(740f, Mathf.Max(370f, host.rect.height - inset - 16f)));
                window.anchoredPosition = new Vector2(0f, inset * .5f);
            }
            if (built) return;
            built = true;
            var title = transform.Find("Title");
            var titleText = title != null ? title.GetComponent<Text>() : null;
            if (titleText != null) titleText.text = "RemoteDesk Pro";
            var font = titleText != null && titleText.font != null
                ? titleText.font : Resources.GetBuiltinResource<Font>("Arial.ttf");
            foreach (string obsolete in new[] { "Scroll View", "Refresh" })
            {
                var child = transform.Find(obsolete);
                if (child != null) child.gameObject.SetActive(false);
            }

            var area = Rect("Offer content", transform);
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.offsetMin = new Vector2(8f, 8f);
            area.offsetMax = new Vector2(-8f, -42f);
            area.gameObject.AddComponent<Image>().color = new Color(1f, .33f, .16f);

            var poster = Rect("RemoteDesk Pro banner", area);
            poster.anchorMin = poster.anchorMax = new Vector2(.5f, .5f);
            poster.pivot = new Vector2(.5f, .5f);
            poster.anchoredPosition = Vector2.zero;
            var artwork = banner != null ? banner : Resources.Load<Sprite>("RemoteDesk/pro_banner");
            var image = poster.gameObject.AddComponent<Image>();
            image.sprite = artwork;
            image.raycastTarget = false;
            var fit = poster.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = artwork != null ? artwork.rect.width / artwork.rect.height : 1718f / 2400f;
            if (artwork == null)
            {
                image.enabled = false;
                Debug.LogError("RemoteDesk Pro artwork is missing from the offer prefab and Resources.");
            }

            // Replace the gray 'кнопка' placeholder on the provided artwork with
            // a real button; its anchors track the poster on window resize.
            var buy = Rect("Купить", poster);
            buy.anchorMin = new Vector2(.53f, .015f);
            buy.anchorMax = new Vector2(.982f, .122f);
            buy.offsetMin = buy.offsetMax = Vector2.zero;
            buy.gameObject.AddComponent<Image>().color = new Color(.8f, .81f, .83f);
            buy.gameObject.AddComponent<Button>().onClick.AddListener(Purchase);
            var label = MakeText("Купить", buy, font, 21, Color.black);
            label.alignment = TextAnchor.MiddleCenter;
            Stretch(label.rectTransform);
            feedback = MakeText("", poster, font, 16, Color.black);
            feedback.alignment = TextAnchor.MiddleCenter;
            feedback.rectTransform.anchorMin = new Vector2(.52f, .125f);
            feedback.rectTransform.anchorMax = new Vector2(.98f, .17f);
            feedback.rectTransform.offsetMin = feedback.rectTransform.offsetMax = Vector2.zero;

            if (artwork == null)
            {
                var missing = MakeText("Не удалось загрузить изображение Pro", area, font, 20, Color.black);
                missing.alignment = TextAnchor.MiddleCenter;
                Stretch(missing.rectTransform);
            }
        }

        private void Purchase()
        {
            string error;
            if (RemoteDesk.TryPurchasePro(system, out error)) Close();
            else if (feedback != null) feedback.text = error;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            obj.layer = 5;
            var rect = (RectTransform)obj.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static Text MakeText(string content, Transform parent, Font font, int size, Color color)
        {
            var value = Rect("Text", parent).gameObject.AddComponent<UnityEngine.UI.Text>();
            value.text = content;
            value.font = font;
            value.fontSize = size;
            value.color = color;
            value.raycastTarget = false;
            return value;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
