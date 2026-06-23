using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software.OS
{
	public class Installer : ComputerSystem
	{
		[SerializeField]
		private GameObject welcome;

		[SerializeField]
		private GameObject information;

		[SerializeField]
		private GameObject installation;

		[SerializeField]
		private ListView storageListView;

		[SerializeField]
		private Button informationNextButton;

		[SerializeField]
		private Slider installProgress;

		[SerializeField]
		private int minimumSpace = 60000;

		[SerializeField]
		private App[] preinstalledApps;

		protected override void BootSystem()
        {
			return;
        }

		public void Information()
		{
			var all = AllStorage;
			var lv = storageListView;
			if (all == null || lv == null) return;

			for (int index = 1; index < all.Count; index++)
			{
				var s = all[index];
				if (s == null) continue;
				var text = string.Format("{0:X8} {1}", s.Id, Conversion.Size(s.Capacity));
				lv.Add(new ListViewItem(text, null));
			}

			lv.SelectedIndexChanged += StorageListView_SelectedIndexChanged;

			if (welcome != null) welcome.SetActive(false);
			if (information != null) information.SetActive(true);

			TakeResource();
		}

		private void StorageListView_SelectedIndexChanged(int index)
		{
			var all = AllStorage;
			if (all == null) return;
			var btn = informationNextButton;
			var storage = (index + 1 >= 0 && index + 1 < all.Count) ? all[index + 1] : null;
			if (storage != null && btn != null) btn.interactable = minimumSpace < storage.Capacity;
		}

		public void Install()
		{
			if (information != null) information.SetActive(false);
			if (installation != null) installation.SetActive(true);
			StartCoroutine(InstallAnimation());
		}

		private IEnumerator InstallAnimation()
		{
			float t = 0f;
			while (t < 1f)
			{
				t += Time.deltaTime / 20f;
				if (installProgress != null) installProgress.value = t;
				yield return null;
			}
			FinishInstall();
		}

		private void FinishInstall()
		{
			var lv = storageListView;
			var all = AllStorage;
			if (lv == null || all == null) return;

			int index = lv.SelectedIndex + 1;
			if (index < 0 || index >= all.Count) return;

			var storage = all[index];
			if (storage == null) return;

			storage.files = new List<File>();

			var boot = new File("System/boot.bin", "pcos", true, minimumSpace);
			if (FileManager != null) FileManager.Create(index, boot);

			var presets = preinstalledApps;
			if (presets != null)
			{
				for (int i = 0; i < presets.Length; i++)
				{
					var app = presets[i];
					if (app == null) continue;
					var file = new File(app.AppName + ".exe", "", false, app.size);
					if (FileManager != null) FileManager.Create(index, file);
				}
			}

			var bios = Board != null ? Board.BiosSettings : null;
			if (bios != null)
			{
				bios.order = new[] { storage.Id };
				if (Board != null) Board.PowerOff(true);
			}
		}

		public override void Fault()
		{
			var board = Board;
			if (board != null) board.PowerOff(false);
		}

		public override void PowerClicked()
		{
			var board = Board;
			if (board != null) board.PowerOff(false);
		}
	}
}
