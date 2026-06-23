using System;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Shop
{
    public class ShopUI : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private Text nameText;
        [SerializeField] private Text priceText;
        [SerializeField] private Text bitcoinText;
        [SerializeField] private Text buttonText;
        [SerializeField] private Button button;
        [SerializeField] private GameObject more;

        private ShopItem item;

        public event Action<ShopItem> OnBuy
        {
            add => onBuy += value;
            remove => onBuy -= value;
        }
        private Action<ShopItem> onBuy;

        public void Init(ShopItem item)
        {
            this.item = item;
			if (button != null)
			{
				button.onClick.AddListener(ButtonDown);
				button.GetComponentInChildren<Text>().text = Localization.GetText("Purchase");
            }
            if (icon != null)
            {
                icon.sprite = item.sprite;
            }
            string displayName = item.itemName;
            if (!string.IsNullOrEmpty(displayName))
            {
                int start = displayName.IndexOf('{');
                int end = displayName.LastIndexOf('}');
                if (start != -1 && end != -1 && end > start)
                {
                    string key = displayName.Substring(start + 1, end - start - 1);
                    string value = Localization.GetText(key);
                    displayName = displayName.Remove(end, 1).Remove(start, 1).Replace(key, value);
                }
            }
            if (nameText != null) nameText.text = displayName;
            if (priceText != null) priceText.text = $"{item.price}$";
            if (bitcoinText != null) bitcoinText.text = $"{item.bitcoin} BTC";
            if (string.IsNullOrEmpty(item.description))
            {
                if (more != null) more.SetActive(false);
            }
            UpdateButton();
        }

        private void ButtonDown()
        {
            var item = this.item;
            if (item == null) return;

            if (item.IsUnlocked())
            {
                onBuy?.Invoke(item);
                return;
            }

            float bitcoin = BitcoinManager.Bitcoin;
            if (item.bitcoin <= bitcoin)
            {
                BitcoinManager.Bitcoin = bitcoin - item.bitcoin;
                BitcoinManager.Save();
                item.Unlock();
                UpdateButton();
                return;
            }

            var main = Main.Instance;
            if (main != null)
            {
                var msg = Localization.GetText("Not enough Bitcoin");
                msg = "<color=red>" + msg + "</color>";
                main.FadeText(msg);
            }
        }

        public void UpdateButton()
        {
            var item = this.item;
            if (item == null) return;

            var priceLabel = priceText;
            if (priceLabel == null || priceLabel.gameObject == null) return;

            bool unlocked = item.IsUnlocked();

            var buttonLabel = buttonText;

            if (unlocked)
            {
                priceLabel.gameObject.SetActive(true);

                if (bitcoinText != null && bitcoinText.gameObject != null)
                    bitcoinText.gameObject.SetActive(false);

                var spawn = item.spawn;
                if (spawn == null)
                {
                    if (button != null) button.interactable = false;
                    if (buttonLabel != null) buttonLabel.text = Localization.GetText("Sold Out");
                }
                else
                {
                    if (button != null) button.interactable = true;
                    if (buttonLabel != null) buttonLabel.text = Localization.GetText("Purchase");
                }
            }
            else
            {
                priceLabel.gameObject.SetActive(false);

                if (bitcoinText != null && bitcoinText.gameObject != null)
                    bitcoinText.gameObject.SetActive(true);

                if (button != null) button.interactable = true;
                if (buttonLabel != null) buttonLabel.text = Localization.GetText("Unlock");
            }
        }

        public void CanBuy(bool canBuy)
        {
            var price = priceText;

            if (canBuy)
            {
                if (price != null)
                {
                    price.color = new Color(0f, 0f, 0f, 1f);
                    var i = item;
                    if (i != null && i.spawn != null)
                    {
                        var btn = button;
                        if (btn != null) btn.interactable = true;
                    }
                }
            }
            else
            {
                if (price != null)
                {
                    price.color = new Color(1f, 0f, 0f, 1f);
                    var btn = button;
                    if (btn != null) btn.interactable = false;
                }
            }
        }

        public void ShowMore()
        {
            var i = item;
            if (i == null) return;

            var text = i.description;
            if (i.translateDescription) text = Localization.GetText(text);

            var main = Main.Instance;
            if (main != null) main.FadeText(text);
        }
    }
}
