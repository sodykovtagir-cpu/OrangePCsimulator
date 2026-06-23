using System;
using GoogleMobileAds.Api;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(AudioSource))]
public class CoinPanel : MonoBehaviour
{
	[SerializeField]
	private InputField cashoutInput;

	[SerializeField]
	private Button cashoutButton;

	[SerializeField]
	private Text estimateText;

	[SerializeField]
	private Text bitcoinText;

	[SerializeField]
	private AudioClip cashSound;

	[SerializeField]
	private int clicksPerSecond;

	[SerializeField]
	private Button freeCoinsButton;

	[SerializeField]
	private Text freeCoinsText;

	[SerializeField]
	private GameObject earnButton;

	private AudioSource source;

	private float bitcoinExchange;

	private float currentBitcoin;

	private int clickCount;

	private float time;

	private void Awake()
	{
		source = GetComponent<AudioSource>();
		var ad = AdManager.Instance;
    	if (ad != null) ad.EarnedReward += EarnedReward;
	}

	private void OnEnable()
	{
		Main.Instance.StopAllControl();
		Refresh();
	}

	private void OnDisable()
	{
		Main.Instance.ResumeAllControl();
	}

	private void OnDestroy()
	{
		var ad = AdManager.Instance;
		if (ad != null) ad.EarnedReward -= EarnedReward;
	}

	private void Update()
	{
		time += Time.deltaTime;

		if (time > 1.0f)
		{
			if (clickCount > clicksPerSecond)
			{
				if (earnButton == null)
				{
					throw new InvalidOperationException();
				}
				earnButton.SetActive(false);
			}
			clickCount = 0;
			time = 0f;
		}
	}

	private void CalculateBalance()
	{
		if (cashoutInput == null)
		{
			throw new InvalidOperationException();
		}

		float inputAmount = float.Parse(cashoutInput.text);
		bitcoinExchange = inputAmount;

		float current = currentBitcoin;
		float maxValue = Math.Min(current, 20.0f);

		if (maxValue < inputAmount || inputAmount < 0.001f)
		{
			inputAmount = maxValue;
			bitcoinExchange = inputAmount;
		}

		if (cashoutButton != null)
		{
			cashoutButton.interactable = inputAmount <= current;

			if (cashoutInput != null)
			{
				cashoutInput.text = inputAmount.ToString("F3");

				if (estimateText != null)
				{
					float exchangedValue = bitcoinExchange * BitcoinManager.exchangeRate;

					int displayValue = float.IsInfinity(exchangedValue) ? int.MinValue : (int)exchangedValue;

					string displayString = "> " + displayValue + "$";

					estimateText.text = displayString;
					return;
				}
			}
		}

		throw new InvalidOperationException();
	}

	private void CashOut()
	{
		if (bitcoinExchange <= 0f)
			return;

		BitcoinManager.Bitcoin = BitcoinManager.Bitcoin - bitcoinExchange;

		var main = Main.Instance;
		if (main != null)
		{
			float exchangedMoney = BitcoinManager.exchangeRate * bitcoinExchange;
			int moneyToAdd = float.IsInfinity(exchangedMoney) ? int.MinValue : (int)exchangedMoney;

			main.SetMoney(main.Money + moneyToAdd, false);

			if (source != null)
			{
				source.PlayOneShot(cashSound);
				Refresh();
				return;
			}
		}

		throw new InvalidOperationException();
	}

	private void Refresh()
	{
		currentBitcoin = BitcoinManager.Bitcoin;
		if (bitcoinText != null)
		{
			bitcoinText.text = currentBitcoin.ToString("F3");
			CalculateBalance();
			return;
		}
		throw new InvalidOperationException();
	}

	public void EarnCoins()
	{
		Main.Instance.AddMoney(5);
		source.PlayOneShot(cashSound);
	}

	public void FreeCoins()
	{
		var ad = AdManager.Instance;
		if (ad == null) return;

		if (freeCoinsText != null)
			freeCoinsText.text = Localization.GetText("Loading...");

		if (freeCoinsButton != null)
			freeCoinsButton.interactable = false;

		Action<bool> rewardedAdCallback = value =>
		{
			if (freeCoinsText != null)
				freeCoinsText.text = Localization.GetText("Free Coins");

			if (freeCoinsButton != null)
				freeCoinsButton.interactable = true;
		};

		ad.CreateAndLoadRewardedAd("FreeCoins", rewardedAdCallback);
	}

	private void EarnedReward(GoogleMobileAds.Api.Reward reward)
	{
		if (reward == null) return;
		if (!string.Equals(reward.Type, "Coin")) return;

		var main = Main.Instance;
		if (main == null) return;

		int amount = (int)reward.Amount;
		main.SetMoney(main.Money + amount, false);
	}
}
