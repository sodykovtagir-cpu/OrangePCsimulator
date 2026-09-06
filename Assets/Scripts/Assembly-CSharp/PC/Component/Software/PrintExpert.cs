using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
	public class PrintExpert : Website
	{
		[SerializeField]
		private TextureLoader bannerPrefab;

		[SerializeField]
		private Text fileNameText;

		[SerializeField]
		private GameObject home;

		[SerializeField]
		private GameObject thankYou;

		[SerializeField]
		private Text alertText;

		[SerializeField]
		private Button purchaseButton;

		[Tooltip("Галочка «HD»: баннер 128×280 за доплату 500$ (обычный 32×70 за 200$).")]
		[SerializeField]
		private Toggle hdToggle;

		private File selectedFile;

		private const int bannerPrice = 200;
		private const int hdSurcharge = 500;

		// Обычный баннер 32×70, HD — в 4 раза больше (128×280).
		private const int bannerW = 32;
		private const int bannerH = 70;
		private const int bannerHdW = 128;
		private const int bannerHdH = 280;

		private bool Hd => hdToggle != null && hdToggle.isOn;
		private int CurrentPrice => bannerPrice + (Hd ? hdSurcharge : 0);
		private int CurrentW => Hd ? bannerHdW : bannerW;
		private int CurrentH => Hd ? bannerHdH : bannerH;

		public void SelectFile()
		{
			var o = os;
			if (o == null) return;

			System.Action<File> cb = file =>
			{
				if (file == null) return;
				if (fileNameText != null) fileNameText.text = file.path;
				selectedFile = file;
				var btn = purchaseButton;
				if (btn != null) btn.interactable = true;
			};

			o.SelectFile(".pic", cb);
		}

		public void Purchase()
		{
			var file = selectedFile;
			if (file == null) return;

			var tex = FormatConverter.StringToTexture(file.content);
			if (tex == null) return;

			int needW = CurrentW;
			int needH = CurrentH;

			if (tex.width == needW && tex.height == needH)
			{
				var m = Main.Instance;
				if (m == null) return;

				int price = CurrentPrice;
				if (m.Money < price)
				{
					var msg = "<color=red>" + "Not enough cash" + "</color>";
					m.FadeText(msg);
					return;
				}

				m.Spend(price);
				var data = ImageConversion.EncodeToPNG(tex);
				tex.Apply(false, true);
				TextureLoader l = Instantiate(bannerPrefab).GetComponent<TextureLoader>();
				var loader = m.InstantDelivery(l.gameObject);
				if (loader == null) return;

				l.SetTexture(tex, data);

				if (home != null) home.SetActive(false);
				if (thankYou != null) thankYou.SetActive(true);
				return;
			}

			var txt = string.Format("Only supports {0}x{1} resolution", needW.ToString(), needH.ToString());
			if (alertText != null) alertText.text = txt;
		}
	}
}
