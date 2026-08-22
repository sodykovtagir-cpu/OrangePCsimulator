using UnityEngine;
using UnityEngine.UI;

public class Giveaway : MonoBehaviour
{
    [SerializeField] private GameObject home;
    [SerializeField] private GameObject thankYou;
    [SerializeField] private Text infoText;
    [SerializeField] private InputField codeInput;
    [SerializeField] private Button claimButton;

    /// <summary>
    /// Промокоды больше не хранятся в клиенте (не светятся в открытом репо).
    /// Валидация и одноразовость (по client-id) выполняются на сервере:
    /// сервер держит список в promos.json, а выдачу помечает в promo_claimed.json.
    /// Верификация результат возвращает количество кэша и BTC.
    /// </summary>
    public void Claim()
    {
        var input = codeInput;
        if (input == null) return;

        string text = input.text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            ShowMessage("Enter a code", false);
            return;
        }

        SetBusy(true);
        var wc = WorkshopClient.Instance;
        if (wc == null)
        {
            var go = new GameObject("WorkshopClient");
            wc = go.AddComponent<WorkshopClient>();
        }

        wc.Redeem(text, HandleResult);
    }

    private void HandleResult(int cash, float btc, string error)
    {
        SetBusy(false);

        if (error != null)
        {
            ShowMessage(ErrorToMessage(error), false);
            return;
        }

        ApplyReward(cash, btc);
        ShowMessage($"Reward: +{cash}$   +{btc} BTC", true);
    }

    private void ApplyReward(int cash, float btc)
    {
        var main = Main.Instance;
        if (main != null && cash != 0)
            main.SetMoney(main.Money + cash, false);

        if (btc != 0f)
            BitcoinManager.Bitcoin = BitcoinManager.Bitcoin + btc;
    }

    private string ErrorToMessage(string err)
    {
        switch (err)
        {
            case "already": return "You already claimed this code";
            case "invalid": return "Invalid code";
            case "missing": return "Enter a code";
            default:        return "Something went wrong. Try again later.";
        }
    }

    private void SetBusy(bool busy)
    {
        if (claimButton != null) claimButton.interactable = !busy;
        if (codeInput != null) codeInput.interactable = !busy;
    }

    private void ShowMessage(string message, bool success)
    {
        if (infoText != null) infoText.text = message;
        if (home != null) home.SetActive(!success);
        if (thankYou != null) thankYou.SetActive(success);
    }
}
