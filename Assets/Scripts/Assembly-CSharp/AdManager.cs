using System;
using System.Runtime.CompilerServices;
using GoogleMobileAds.Api;
using UnityEngine;
//ad this yourself
public class AdManager : MonoBehaviour
{
	[Serializable]
	private struct RewardedId
	{
		public string name;

		public string idAndroid;

		public string idIOS;
	}

	[Header("Banner")]
	[SerializeField]
	private string bannerIdAndroid;

	[SerializeField]
	private string bannerIdIOS;

	[SerializeField]
	private AdPosition bannerPosition;

	[SerializeField]
	private bool autoLoadBanner;

	[Header("Interstitial")]
	[SerializeField]
	private string interstitialIdAndroid;

	[SerializeField]
	private string interstitialIdIOS;

	[SerializeField]
	private bool autoLoadInterstitial;

	[Header("Rewarded")]
	[SerializeField]
	private RewardedId[] rewardedIds;

	[Header("Test")]
	[SerializeField]
	private bool testAds;

	private BannerView bannerView;

	private InterstitialAd interstitialAd;

	private RewardedAd rewardedAd;

	private Action<bool> rewardedAdCallback;

	public static AdManager Instance { get; private set; }

	public bool NoAds { get; private set; }

	public event Action<Reward> EarnedReward
	{
		[CompilerGenerated]
		add
		{
		}
		[CompilerGenerated]
		remove
		{
		}
	}

	private void Awake()
	{
	}

	private void Init()
	{
	}

	private string BannerId()
	{
		return null;
	}

	public void RequestBanner()
	{
	}

	public void HideBanner(bool hide)
	{
	}

	public void SetBannerPosition(AdPosition adPosition)
	{
	}

	public void DestroyBanner()
	{
	}

	private string InterstitialId()
	{
		return null;
	}

	public void RequestInterstitial()
	{
	}

	public void ShowInterstitial()
	{
	}

	public void CreateAndLoadRewardedAd(string name, Action<bool> rewardedAdCallback = null)
	{
	}

	private void HandleRewardedAdLoaded()
	{
	}

	private void HandleRewardedAdFailedToLoad()
	{
	}

	public void RemoveAds()
    {
        PlayerPrefs.SetInt("NoAds", 1);
    }
}
