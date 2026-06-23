using System.Collections.Generic;
using UnityEngine;

namespace PC.Component.Software.OS
{
	public abstract class ComputerSystem : MonoBehaviour
	{
		private int occupied = 1;

		public Motherboard Board { get; private set; }

		public FileManager FileManager { get; private set; }

		public List<Storage> AllStorage { get; private set; }

		public void Init(Motherboard board, int ownedStorage)
		{
			Board = board;

			var src = board != null ? board.GetHardwares(HardwareType.Drive) : null;
			var list = new List<Storage>();
			if (src != null)
			{
				for (int i = 0; i < src.Count; i++)
				{
					var s = src[i] as Storage;
					if (s != null) list.Add(s);
				}
			}
			AllStorage = list;

			if (ownedStorage != -1 && list.Count > 0)
			{
				int idx = -1;
				for (int i = 0; i < list.Count; i++)
				{
					if (list[i].Id == ownedStorage)
					{
						idx = i;
						break;
					}
				}
				if (idx > 0)
				{
					var v0 = list[0];
					list[0] = list[idx];
					list[idx] = v0;
				}
			}

			FileManager = new FileManager(this);
			BootSystem();
		}
		protected abstract void BootSystem();

		public abstract void PowerClicked();

		public abstract void Fault();

		public void AddHardware(Hardware hardware)
		{
			if (AllStorage == null) return;
			if (hardware is Storage s) AllStorage.Add(s);
		}

		public void RemoveHardware(Hardware hardware)
		{
			if (AllStorage == null) return;
			var s = hardware as Storage;
			if (s == null) return;

			int index = AllStorage.IndexOf(s);
			if (index < 0) return;

			if (index < occupied) occupied--;
			AllStorage.RemoveAt(index);
		}

		public void TakeResource()
		{
			if (AllStorage == null) return;
			occupied = AllStorage.Count;
		}

		public void ReleaseResource()
        {
			occupied = 1;
        }
	}
}
