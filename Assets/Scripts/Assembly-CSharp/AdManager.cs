using UnityEngine;

// Реклама полностью вырезана (см. BUGS_REPORT.md, раздел «Реклама»).
// Класс оставлен как пустая заглушка, чтобы не ломать ссылки в сценах
// (GameObject с AdManager) и вызовы из Display/ShopPanel/StoreMenu.
public class AdManager : MonoBehaviour
{
	public static AdManager Instance { get; private set; }

	public bool NoAds { get; private set; }

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Destroy(gameObject);
			return;
		}
		Instance = this;
		NoAds = PlayerPrefs.GetInt("NoAds", 0) == 1;
	}

	// --- Заглушки (вызываются из существующих скриптов/кнопок) ---

	public void RequestBanner() { }
	public void HideBanner(bool hide) { }
	public void SetBannerPosition() { }
	public void DestroyBanner() { }
	public void RequestInterstitial() { }
	public void ShowInterstitial() { }

	public void CreateAndLoadRewardedAd(string name, System.Action<bool> callback = null)
	{
		if (callback != null) callback(false);
	}

	public void RemoveAds()
	{
		PlayerPrefs.SetInt("NoAds", 1);
		PlayerPrefs.Save();
		NoAds = true;
	}
}
