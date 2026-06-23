using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
	public class Info : App
	{
		[SerializeField]
		private Text cpuText;

		[SerializeField]
		private Text ramText;

		[SerializeField]
		private Text storageText;

		[SerializeField]
		private GameObject[] gpuInfo;

		[SerializeField]
		private Text cpuTempText;

		private Text[] gpuTexts;

		protected override void Start()
		{
			base.Start();
			var infos = gpuInfo;
			if (infos == null) return;

			gpuTexts = new Text[infos.Length];
			for (int i = 0; i < infos.Length; i++)
			{
				var go = infos[i];
				if (go == null) break;
				gpuTexts[i] = go.GetComponentInChildren<Text>();
			}

			StartCoroutine(ShowInfo());
		}

		private IEnumerator ShowInfo()
		{
			var sys = system;
			var board = sys != null ? sys.Board : null;

			var cpuList = board != null ? board.GetHardwares(HardwareType.CPU) : null;
			var cpu = (CPU)((cpuList != null && cpuList.Count > 0) ? cpuList[0] : null);

			if (cpuText != null && cpu != null) cpuText.text = cpu.Info;

			int ram = 0;
			var rams = board != null ? board.GetHardwares(HardwareType.RAM) : null;
			if (rams != null) for (int i = 0; i < rams.Count; i++) { var h = rams[i]; if (h != null) ram += h.Capacity; }
			if (ramText != null) ramText.text = Conversion.Size(ram);

			int storage = 0;
			var all = sys != null ? sys.AllStorage : null;
			if (all != null) for (int i = 0; i < all.Count; i++) { var s = all[i]; if (s != null) storage += s.Capacity; }
			if (storageText != null) storageText.text = Conversion.Size(storage);

			var slots = board != null ? board.GetSlots(HardwareType.GPU) : null;
			var infos = gpuInfo;
			var texts = gpuTexts;
			if (infos != null)
			{
				for (int i = 0; i < infos.Length; i++)
				{
					var go = infos[i];
					if (go == null) break;
					if (slots != null && i < slots.Count)
					{
						go.SetActive(true);
						var t = texts != null && i < texts.Length ? texts[i] : null;
						if (t != null)
						{
							var slot = slots[i];
							t.text = (slot != null && slot.Hardware != null) ? slot.Hardware.Info : Localization.GetText("(Empty)");
						}
					}
					else
					{
						go.SetActive(false);
					}
				}
			}

			while (true)
			{
				if (cpu != null && cpuTempText != null) cpuTempText.text = cpu.temperature.ToString("0") + "°C";
				yield return new WaitForSeconds(0.5f);
			}
		}
	}
}
