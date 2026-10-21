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
        private bool initialized;
        private Coroutine miningRoutine;

        private void StopMining()
        {
            if (system != null && miningRoutine != null)
            {
                system.StopCoroutine(miningRoutine);
                miningRoutine = null;
            }
        }

        public override void Close()
        {
            StopMining();
            base.Close();
        }

        public override void OnSystemStop()
        {
            StopMining();
            base.OnSystemStop();
        }

        private void OnDestroy() { StopMining(); }

        // Launch parameters: "bitcoin" (BTC), "cash"/"money" (dollars).
        // Open is called before Start on newly instantiated Unity windows.
        public override void Open(string content)
        {
            base.Open(content);
            if (string.Equals(content, "bitcoin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(content, "btc", StringComparison.OrdinalIgnoreCase))
                bitcoinMode = true;
            else if (string.Equals(content, "cash", StringComparison.OrdinalIgnoreCase)
                || string.Equals(content, "money", StringComparison.OrdinalIgnoreCase))
                bitcoinMode = false;
            if (initialized) ChangeMode(bitcoinMode);
        }

		protected override void Start()
		{
			base.Start();

			var main = Main.Instance;
			if (main != null && main.example && bitcoin_button != null) bitcoin_button.interactable = false;

			score = 0;

			var os = system;
			var board = os != null ? os.Board : null;

            // Mine on the discrete GPU if present, otherwise on the CPU.
            // Do not mix both scores or tie payout to the OS input lag.
            bool hasGpu = os != null && os.HasDiscreteGpu;
            var parts = board != null ? board.GetHardwares(hasGpu ? HardwareType.GPU : HardwareType.CPU) : null;
            if (parts != null)
                foreach (var h in parts)
                    if (h != null && h.Power && !h.Damaged)
                        score += Mathf.RoundToInt(h.Score * (hasGpu
                            ? PC.Component.Software.OS.OverclockBoot.GpuMultiplier(board) : 1f));

            initialized = true;
			ChangeMode(bitcoinMode);
            // OS owns the coroutine: Unity deactivates minimized windows and
            // would stop a coroutine running on Miner, interrupting payouts.
            miningRoutine = system != null ? system.StartCoroutine(Digging()) : StartCoroutine(Digging());
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

			while (system != null && system.Ready)
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
