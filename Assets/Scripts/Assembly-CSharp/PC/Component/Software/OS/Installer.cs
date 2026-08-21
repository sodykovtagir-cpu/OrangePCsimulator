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

		private readonly List<int> installTargets = new List<int>();
		private bool listHooked;

		protected override void BootSystem()
        {
			return;
        }

		public void Information()
		{
			var all = AllStorage;
			var lv = storageListView;
			if (all == null || lv == null) return;

			lv.Clear();
			installTargets.Clear();

			for (int index = 0; index < all.Count; index++)
			{
				var s = all[index];
				if (s == null) continue;
				// Skip installer boot media only when there is another disk to install to.
				if (index == 0 && all.Count > 1) continue;
				var text = string.Format("{0:X8} {1}", s.Id, Conversion.Size(s.Capacity));
				lv.Add(new ListViewItem(text, null));
				installTargets.Add(index);
			}

			if (!listHooked)
			{
				lv.SelectedIndexChanged += StorageListView_SelectedIndexChanged;
				listHooked = true;
			}

			if (informationNextButton != null)
				informationNextButton.interactable = false;

			if (welcome != null) welcome.SetActive(false);
			if (information != null) information.SetActive(true);

			TakeResource();
		}

		private int StorageIndexFromList(int listIndex)
		{
			if (listIndex < 0 || listIndex >= installTargets.Count) return -1;
			return installTargets[listIndex];
		}

		private void StorageListView_SelectedIndexChanged(int index)
		{
			var all = AllStorage;
			if (all == null) return;
			var btn = informationNextButton;
			int si = StorageIndexFromList(index);
			var storage = (si >= 0 && si < all.Count) ? all[si] : null;
			if (btn != null) btn.interactable = storage != null && minimumSpace < storage.Capacity;
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
