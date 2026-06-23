using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software.OS
{
	public class Bios : ComputerSystem
	{
		[Serializable]
		public class BiosSettings
		{
			public int[] order;
		}

		private struct BootPosition
		{
			public int id;

			public int storage;

			public string type;
		}

		[SerializeField]
		private OperatingSystem osPrefab;

		[SerializeField]
		private Installer installerPrefab;

		[SerializeField]
		private GameObject startup;

		[SerializeField]
		private GameObject utility;

		[SerializeField]
		private Button selectionPrefab;

		[SerializeField]
		private Transform selectionParent;

		private bool enterBios;

		private int selectedStorage;

		private List<BootPosition> bootTypes;

		private Color selectionColor;

		public const string bootFile = "System/boot.bin";

		protected override void BootSystem()
		{
			Invoke("BootOperatingSystem", 2f);
        }

		private void BootOperatingSystem()
		{
			bootTypes = FindBootFile();

			if (enterBios)
			{
				EnterBiosSettings();
				return;
			}

			if (bootTypes == null || bootTypes.Count == 0)
			{
				EnterBiosSettings();
				return;
			}

			var first = bootTypes[0];

			if (first.type == "pcos")
			{
				var board = Board;
				var storage = first.storage;
				if (board != null && osPrefab != null)
					board.SwitchSystem(storage, osPrefab);
				return;
			}

			if (first.type == "pcos_ins")
			{
				var board = Board;
				var storage = first.storage;
				if (board != null && installerPrefab != null)
					board.SwitchSystem(storage, installerPrefab);
				return;
			}

			EnterBiosSettings();
		}

		private void EnterBiosSettings()
		{
			TakeResource();

			var btn = selectionPrefab;
			if (btn == null) return;

			var txt = btn.GetComponent<Text>();
			if (txt == null) return;

			selectionColor = txt.color;

			Refresh();

			var go = startup;
			if (go != null) go.SetActive(false);

			go = utility;
			if (go != null) go.SetActive(true);
		}

		private void Refresh()
		{
			var parent = selectionParent;
			if (parent == null) return;

			for (int i = parent.childCount - 1; i >= 0; i--)
				Destroy(parent.GetChild(i).gameObject);

			var list = bootTypes;
			if (list == null || list.Count == 0) return;

			for (int i = 0; i < list.Count; i++)
			{
				var boot = list[i];
				var btn = Instantiate(selectionPrefab, parent);
				if (btn == null) continue;

				var txt = btn.GetComponent<UnityEngine.UI.Text>();
				if (txt != null)
				{
					txt.color = (i == selectedStorage) ? Color.white : selectionColor;
					txt.text = $"{i + 1}. {boot.storage:X8}\n{boot.type}";
				}

				int idx = i;
				btn.onClick.AddListener(() =>
				{
					selectedStorage = idx;
					Refresh();
				});
			}
		}

		private List<BootPosition> FindBootFile()
		{
			var result = new List<BootPosition>();
			var board = Board;
			if (board == null) return result;

			var drives = board.GetHardwares(HardwareType.Drive);
			if (drives != null)
			{
				int ida = 0;
				foreach (var hw in drives)
				{
					if (hw is Storage s && s.TryGetFile("System/boot.bin", out var file))
						result.Add(new BootPosition { id = ida, storage = s.Id, type = file.content });
					ida++;
				}
			}

			var settings = board.BiosSettings;
			if (settings != null) SortArrayByTarget(result, settings.order);
			return result;
		}

		private void SortArrayByTarget(List<BootPosition> unsortedArray, int[] targetOrder)
		{
			if (unsortedArray == null || targetOrder == null) return;
			var indexByStorage = new Dictionary<int,int>(targetOrder.Length);
			for (int i = 0; i < targetOrder.Length; i++) indexByStorage[targetOrder[i]] = i;

			unsortedArray.Sort((a, b) =>
			{
				int ai = indexByStorage.TryGetValue(a.storage, out var ia) ? ia : int.MaxValue;
				int bi = indexByStorage.TryGetValue(b.storage, out var ib) ? ib : int.MaxValue;
				int c = ai.CompareTo(bi);
				if (c != 0) return c;
				return string.CompareOrdinal(a.type, b.type);
			});
		}

		public void MoveUp()
		{
			if (selectedStorage <= 0 || bootTypes == null || selectedStorage >= bootTypes.Count) return;
			var i = selectedStorage;
			(bootTypes[i - 1], bootTypes[i]) = (bootTypes[i], bootTypes[i - 1]);
			selectedStorage--;
			Refresh();
		}

		public void MoveDown()
		{
			if (bootTypes == null || selectedStorage < 0 || selectedStorage >= bootTypes.Count - 1) return;
			var i = selectedStorage;
			(bootTypes[i], bootTypes[i + 1]) = (bootTypes[i + 1], bootTypes[i]);
			selectedStorage++;
			Refresh();
		}

		public void Exit()
		{
			var board = Board;
			if (board == null) return;
			board.PowerOff(true);
		}

		public void SaveAndExit()
		{
			var board = Board;
			var list = bootTypes;
			if (board == null || list == null) return;

			var settings = board.BiosSettings;
			if (settings == null) return;

			settings.order = new int[list.Count];
			for (int i = 0; i < list.Count; i++)
				settings.order[i] = list[i].id;

			Exit();
		}

		public override void Fault()
		{
			var board = Board;
			if (board != null)
				board.PowerOff(false);
		}

		public override void PowerClicked()
		{
			if (utility == null) return;
			if (!utility.activeSelf)
			{
				enterBios = true;
				return;
			}
			var board = Board;
			if (board != null) board.PowerOff(false);
		}
	}
}
