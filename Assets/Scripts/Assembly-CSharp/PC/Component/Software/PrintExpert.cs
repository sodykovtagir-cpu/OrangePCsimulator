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

		private File selectedFile;

		private const int bannerPrice = 200;

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

			if (tex.width == 32 && tex.height == 70)
			{
				var m = Main.Instance;
				if (m == null) return;

				if (m.Money < bannerPrice)
				{
					var msg = "<color=red>" + "Not enough cash" + "</color>";
					m.FadeText(msg);
					return;
				}

				m.Spend(bannerPrice);
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

			var txt = string.Format("Only supports {0}x{1} resolution", "32", "70");
			if (alertText != null) alertText.text = txt;
		}
	}
}
