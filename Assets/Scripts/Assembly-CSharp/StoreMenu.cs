using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.UI;

public class StoreMenu : MonoBehaviour
{
	[SerializeField]
	private Button removeAdsButton;

	[SerializeField]
	private Button moreCoinButton;

	[SerializeField]
	private GameObject quiz;

	private void Start()
	{
		Localization.LanguageChanged += RefreshStore;
		RefreshStore();
	}

	private void OnDestroy()
	{
		Localization.LanguageChanged -= RefreshStore;
	}

	private void PurchaseComplete(Product product)
    {
        if (product?.definition == null) return;

        string id = product.definition.id;

        if (id == "no_ads")
        {
            AdManager.Instance?.RemoveAds();
            if (quiz != null) quiz.SetActive(false);
        }
        else if (id == "more_coin")
        {
            PlayerPrefs.SetInt("MoreCoin", 1);
        }
        else if (id == "btc_50" || id == "btc_100" || id == "btc_200")
        {
            float add =
                id == "btc_50"  ?  50f :
                id == "btc_100" ? 100f : 200f;
            BitcoinManager.Bitcoin += add;
            BitcoinManager.Save();
        }

        RefreshStore();
    }

	private void RefreshStore()
	{
		bool boughtRemoveAds = PlayerPrefs.GetInt("NoAds", 0) == 1;
		bool boughtMoreCoin = PlayerPrefs.GetInt("MoreCoin", 0) == 1;
		if (boughtRemoveAds)
		{
			removeAdsButton.interactable = false;
			removeAdsButton.GetComponent<Text>().text = string.Format("{0}", Localization.GetText("Bought"));
		}
		else
		{
			removeAdsButton.interactable = true;
			removeAdsButton.GetComponent<Text>().text = string.Format("[{0}]", Localization.GetText("Buy"));
		}
		if (boughtMoreCoin)
		{
			moreCoinButton.interactable = false;
			moreCoinButton.GetComponent<Text>().text = string.Format("{0}", Localization.GetText("Bought"));
		}
		else
		{
			moreCoinButton.interactable = true;
			moreCoinButton.GetComponent<Text>().text = string.Format("[{0}]", Localization.GetText("Buy"));
		}
	}
}
