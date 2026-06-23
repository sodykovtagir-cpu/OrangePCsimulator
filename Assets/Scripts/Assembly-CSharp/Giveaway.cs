using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class Giveaway : MonoBehaviour
{
	private class Request
	{
		public string code;

		public string to;
	}

	private class Response
	{
		public int cash;

		public float btc;
	}

	[SerializeField]
	private GameObject home;

	[SerializeField]
	private GameObject thankYou;

	[SerializeField]
	private Text infoText;

	[SerializeField]
	private InputField codeInput;

	[SerializeField]
	private Button claimButton;

	[SerializeField]
	private string url;

	private static List<string> claimedCodes;

	public void Claim()
	{
		var input = codeInput;
		if (input == null) return;
		var text = input.text;
		if (IsClaimed(text))
		{
			if (infoText != null) infoText.text = "You already claimed this code";
			if (home != null) home.SetActive(false);
			if (thankYou != null) thankYou.SetActive(true);
			return;
		}
		var routine = RequestGiveaway(text);
		StartCoroutine(routine);
	}

	private IEnumerator RequestGiveaway(string code)
	{
		if (claimButton != null) claimButton.interactable = false;
		var fullUrl = url + "/" + code;
		var www = UnityWebRequest.Get(fullUrl);
		yield return www.SendWebRequest();
		if (www.result == UnityWebRequest.Result.Success)
		{
			var text = www.downloadHandler.text;
			if (!string.IsNullOrEmpty(text))
			{
				var resp = JsonUtility.FromJson<Response>(text);
				var input = codeInput;
				Claim(input.text, resp);
			}
		}
		else
		{
			UnityEngine.Debug.Log(www.error);
		}
		if (home != null) home.SetActive(false);
		if (thankYou != null) thankYou.SetActive(true);
	}

	public bool IsClaimed(string code)
	{
		if (claimedCodes == null)
		{
			var saved = PlayerPrefs.GetString("Giveaway");
			if (!string.IsNullOrEmpty(saved))
				claimedCodes = new List<string>(saved.Split(','));
			else
				claimedCodes = new List<string>();
		}
		return claimedCodes.Contains(code);
	}

	private void Claim(string code, Response response)
	{
		if (claimedCodes == null) claimedCodes = new List<string>();
		claimedCodes.Add(code);
		var value = string.Join(",", claimedCodes);
		PlayerPrefs.SetString("Giveaway", value);
		if (response == null) return;
		var main = Main.Instance;
		if (main == null) return;
		main.SetMoney(main.Money + response.cash, false);
		BitcoinManager.Bitcoin = BitcoinManager.Bitcoin + response.btc;
	}
}
