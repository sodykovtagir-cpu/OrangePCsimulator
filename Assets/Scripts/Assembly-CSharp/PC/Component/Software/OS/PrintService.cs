using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software.OS
{
	public class PrintService : MonoBehaviour
	{
		[SerializeField]
		private GameObject general;

		[SerializeField]
		private GameObject supplyLevels;

		[SerializeField]
		private Text alertText;

		[SerializeField]
		private ListView printerListView;

		[SerializeField]
		private Text copiesText;

		[SerializeField]
		private Slider inkY;

		[SerializeField]
		private Slider inkM;

		[SerializeField]
		private Slider inkC;

		[SerializeField]
		private Slider inkK;

		[SerializeField]
		private Button printButton;

		[SerializeField]
		private Button supplyButton;

		[SerializeField]
		private Image colorImage;

		[SerializeField]
		private Image grayscaleImage;

		private Texture2D tex;

		private int copies = 1;

		private bool grayscale;

		private OperatingSystem os;

		private List<DeviceDetail> devices;

		private const int deviceType = 1;

		public void Show(OperatingSystem os, Texture2D tex)
		{
			this.os = os;
			this.tex = tex;

			if (os == null) return;

			var allDevices = os.ListInstalledDevices();
			devices = new List<DeviceDetail>();

			foreach (var d in allDevices)
			{
				if (d != null && d.type == deviceType) devices.Add(d);
			}

			foreach (var d in devices)
			{
				var item = new ListViewItem(d.name);
				printerListView.Add(item);
			}

			printerListView.SelectedIndexChanged += SelectIndexChanged;
		}

		private void SelectIndexChanged(int index)
		{
			if (supplyButton != null) supplyButton.interactable = index != -1;

			var t = tex;
			if (t != null)
			{
				if (t.width == 32 && t.height == 32)
				{
					if (printButton != null) printButton.interactable = index != -1;
					return;
				}

				var msg = string.Format(Localization.GetText("Only supports {0}x{1} resolution"), "32", "32");
				if (alertText != null) alertText.text = msg;
			}
		}

		public void SetGrayscale(bool grayscale)
		{
			this.grayscale = grayscale;
			var color = colorImage;
			if (color != null)
			{
				color.enabled = !grayscale;
				var gs = grayscaleImage;
				if (gs != null) gs.enabled = grayscale;
			}
		}

		public void IncreasePaper()
		{
			copies++;
			if (copiesText != null) copiesText.text = copies.ToString();
		}

		public void DecreasePaper()
		{
			if (copies > 1) copies--;
			if (copiesText != null) copiesText.text = copies.ToString();
		}

		public void Print()
		{
			var list = printerListView;
			var devs = devices;
			var system = os;
			if (list == null || devs == null || system == null) return;

			int index = list.SelectedIndex;
			if (index < 0 || index >= devs.Count) return;

			var detail = devs[index];
			var printer = system.ConnectDevice<Printer>(detail.id);
			if (printer != null)
			{
				printer.PrintPicture(tex, grayscale, copies);
				Destroy(gameObject);
				return;
			}

			var title = Localization.GetText("Error");
			var message = Localization.GetText("Unable to connect to device");
			system.ShowMessageBox(title, message);
		}

		public void Cancel()
		{
			Destroy(gameObject);
		}

		public void ViewSupply()
		{
			var lv = printerListView;
			var list = devices;
			var system = os;
			if (lv == null || list == null || system == null) return;

			int index = lv.SelectedIndex;
			if (index < 0 || index >= list.Count) return;

			var detail = list[index];
			var printer = system.ConnectDevice<Printer>(detail.id);
			if (printer != null)
			{
				var r = printer.remainingInk;
				var t = printer.totalInk;

				if (inkY != null) inkY.value = t.y > 0f ? r.y / t.y : 0f;
				if (inkM != null) inkM.value = t.m > 0f ? r.m / t.m : 0f;
				if (inkC != null) inkC.value = t.c > 0f ? r.c / t.c : 0f;
				if (inkK != null) inkK.value = t.k > 0f ? r.k / t.k : 0f;

				if (general != null) general.SetActive(false);
				if (supplyLevels != null) supplyLevels.SetActive(true);
				return;
			}

			var title = Localization.GetText("Error");
			var message = Localization.GetText("Unable to connect to device");
			system.ShowMessageBox(title, message);
		}

		public void Back()
		{
			if (general != null) general.SetActive(true);
			if (supplyLevels != null) supplyLevels.SetActive(false);
		}
	}
}
