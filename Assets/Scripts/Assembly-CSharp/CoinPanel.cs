using System;
using System.Globalization;
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
				// Не кидаем исключение: earnButton может быть не назначен
				// в инспекторе — это не должно ронять игру.
				if (earnButton != null)
					earnButton.SetActive(false);
			}
			clickCount = 0;
			time = 0f;
		}
	}

	private void CalculateBalance()
	{
		if (cashoutInput == null)
			return;

		// TryParse + InvariantCulture: float.Parse падал на нечисловом вводе,
		// а в русской локали "1.5" разбиралась как 15 (запятая = разделитель).
		float inputAmount;
		if (!float.TryParse(cashoutInput.text, NumberStyles.Float, CultureInfo.InvariantCulture, out inputAmount))
			inputAmount = 0f;
		bitcoinExchange = inputAmount;

		float current = currentBitcoin;
		float maxValue = Math.Min(current, 20.0f);

		if (maxValue < inputAmount || inputAmount < 0.001f)
		{
			inputAmount = maxValue;
			bitcoinExchange = inputAmount;
		}

		if (cashoutButton != null && cashoutInput != null && estimateText != null)
		{
			cashoutButton.interactable = inputAmount <= current;

			cashoutInput.text = inputAmount.ToString("F3", CultureInfo.InvariantCulture);

			float exchangedValue = bitcoinExchange * BitcoinManager.exchangeRate;

			int displayValue = float.IsInfinity(exchangedValue) ? int.MinValue : (int)exchangedValue;

			string displayString = "> " + displayValue + "$";

			estimateText.text = displayString;
		}
	}

	private void CashOut()
	{
		if (bitcoinExchange <= 0f)
			return;

		BitcoinManager.Bitcoin = BitcoinManager.Bitcoin - bitcoinExchange;

		var main = Main.Instance;
		if (main == null || source == null)
			return;

		float exchangedMoney = BitcoinManager.exchangeRate * bitcoinExchange;
		int moneyToAdd = float.IsInfinity(exchangedMoney) ? int.MinValue : (int)exchangedMoney;

		main.SetMoney(main.Money + moneyToAdd, false);
		source.PlayOneShot(cashSound);
		Refresh();
	}

	private void Refresh()
	{
		currentBitcoin = BitcoinManager.Bitcoin;
		if (bitcoinText == null)
			return;
		bitcoinText.text = currentBitcoin.ToString("F3", CultureInfo.InvariantCulture);
		CalculateBalance();
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
