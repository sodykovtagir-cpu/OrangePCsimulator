using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Giveaway : MonoBehaviour
{
    [Serializable]
    private class Promo
    {
        public string code;
        public int cash;
        public float btc;
    }

    [SerializeField] private GameObject home;
    [SerializeField] private GameObject thankYou;
    [SerializeField] private Text infoText;
    [SerializeField] private InputField codeInput;
    [SerializeField] private Button claimButton;

    // Список промокодов (можно редактировать в инспекторе)
    [SerializeField]
    private Promo[] promos = new Promo[]
    {
        new Promo { code = "Orange",                 cash = 100000, btc = 0f },
        new Promo { code = "semyalol",               cash = 0,      btc = 5f },
        new Promo { code = "Testertest09009990122",  cash = 1000000,btc = 10000f },
        new Promo { code = "BigMiner",               cash = 500,    btc = 0f },
        new Promo { code = "Taskbar",                cash = 5000,   btc = 1f },
        new Promo { code = "Gallery",                cash = 0,      btc = 10f },
    };

    private static List<string> claimedCodes;

    public void Claim()
    {
        var input = codeInput;
        if (input == null) return;

        string text = input.text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            ShowMessage("Enter a code");
            return;
        }

        // Уже активирован?
        if (IsClaimed(text))
        {
            ShowMessage("You already claimed this code");
            return;
        }

        // Ищем промокод (с игнорированием регистра)
        Promo found = null;
        for (int i = 0; i < promos.Length; i++)
        {
            if (promos[i] != null &&
                string.Equals(promos[i].code, text, StringComparison.OrdinalIgnoreCase))
            {
                found = promos[i];
                break;
            }
        }

        if (found == null)
        {
            ShowMessage("Invalid code");
            return;
        }

        // Активируем
        ApplyReward(found);
        SaveClaimed(found.code);
        ShowMessage($"Reward: +{found.cash}$   +{found.btc} BTC");
    }

    private void ApplyReward(Promo promo)
    {
        var main = Main.Instance;
        if (main != null && promo.cash != 0)
            main.SetMoney(main.Money + promo.cash, false);

        if (promo.btc != 0f)
            BitcoinManager.Bitcoin = BitcoinManager.Bitcoin + promo.btc;
    }

    private void ShowMessage(string message)
    {
        if (infoText != null) infoText.text = message;
        if (home != null) home.SetActive(false);
        if (thankYou != null) thankYou.SetActive(true);
    }

    public bool IsClaimed(string code)
    {
        LoadClaimed();
        // сравниваем без учёта регистра
        for (int i = 0; i < claimedCodes.Count; i++)
        {
            if (string.Equals(claimedCodes[i], code, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private void SaveClaimed(string code)
    {
        LoadClaimed();
        claimedCodes.Add(code);
        PlayerPrefs.SetString("Giveaway", string.Join(",", claimedCodes));
        PlayerPrefs.Save();
    }

    private static void LoadClaimed()
    {
        if (claimedCodes != null) return;

        var saved = PlayerPrefs.GetString("Giveaway", "");
        claimedCodes = !string.IsNullOrEmpty(saved)
            ? new List<string>(saved.Split(','))
            : new List<string>();
    }
}