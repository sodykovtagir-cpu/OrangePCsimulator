using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
	public class Miner : App
	{
		[SerializeField]
		private Image typeIcon;

		[SerializeField]
		private Button bitcoin_button;

		[SerializeField]
		private Sprite bitcoin_sprite;

		[SerializeField]
		private Sprite cash_sprite;

		[SerializeField]
		private Text balance_text;

		[SerializeField]
		private Text speed_text;

		[SerializeField]
		private Text info_text;

		private int score;

		private bool bitcoinMode;

		private float revenue;

		protected override void Start()
		{
			base.Start();

			var main = Main.Instance;
			if (main != null && main.example && bitcoin_button != null) bitcoin_button.interactable = false;

			score = 0;

			var os = system;
			var board = os != null ? os.Board : null;

			var gpus = board != null ? board.GetHardwares(HardwareType.GPU) : null;
			if (gpus != null)
			{
				for (int i = 0; i < gpus.Count; i++)
				{
					var h = gpus[i];
					if (h != null) score += h.Score;
				}
			}

			var cpus = board != null ? board.GetHardwares(HardwareType.CPU) : null;
			if (cpus != null)
			{
				for (int i = 0; i < cpus.Count; i++)
				{
					var h = cpus[i];
					if (h != null) score += h.Score;
				}
			}

			ChangeMode(false);
			StartCoroutine(Digging());
		}

		public void ChangeMode(bool bitcoinMode)
		{
			this.bitcoinMode = bitcoinMode;

			var bal = balance_text;
			var spd = speed_text;
			var icon = typeIcon;

			if (bitcoinMode)
			{
				if (bal != null) bal.text = BitcoinManager.Bitcoin.ToString("0.####");
				var rev = (float)score / 10000000f;
				revenue = rev;
				if (spd != null) spd.text = rev.ToString() + " BTC/s";
				if (icon != null) icon.sprite = bitcoin_sprite;
				return;
			}

			var main = Main.Instance;
			if (bal != null && main != null) bal.text = main.Money.ToString();
			var r = score / 2000;
			if (r < 2) r = 1;
			revenue = r;
			if (spd != null) spd.text = ((float)r).ToString() + " $/s";
			if (icon != null) icon.sprite = cash_sprite;
		}

		private IEnumerator Digging()
		{
			if (info_text != null)
			{
				var t = Localization.GetText("Start Miner");
				info_text.text = "> " + t;
			}

			yield return new WaitForSeconds(5f);

			if (info_text != null)
			{
				var m = Localization.GetText("Mining...");
				info_text.text = info_text.text + "\n" + m;
			}

			while (true)
			{
				if (bitcoinMode)
				{
					BitcoinManager.Bitcoin += revenue;
					if (balance_text != null) balance_text.text = BitcoinManager.Bitcoin.ToString("0.####");
				}
				else
				{
					var main = Main.Instance;
					if (main != null)
					{
						main.AddMoney((int)revenue);
						if (balance_text != null) balance_text.text = main.Money.ToString();
					}
				}

				yield return new WaitForSeconds(1f);
			}
		}
	}
}
