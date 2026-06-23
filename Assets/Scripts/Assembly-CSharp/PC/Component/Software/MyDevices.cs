using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using PC.Component.Software.OS;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
	public class MyDevices : App
	{
		[Serializable]
		private class DeviceIcon
		{
			public int type;

			public Sprite icon;
		}

		[SerializeField]
		private DeviceItem deviceItemPrefab;

		[SerializeField]
		private Transform deviceItemParent;

		[SerializeField]
		private GameObject loading;

		[SerializeField]
		private DeviceIcon[] deviceIcons;

		[SerializeField]
		private Sprite unknownDeviceIcon;

		[SerializeField]
		private InputField deviceNameInput;

		private readonly List<DeviceItem> selections = new List<DeviceItem>();

		private DeviceItem selected;

		public override void Open(string content)
		{
			base.Open(content);
			var sys = system;
			if (sys == null) return;

			var devices = sys.ListInstalledDevices();
			if (devices == null) return;

			for (int i = 0; i < devices.Count; i++)
			{
				var d = devices[i];
				AddToList(d, "-", true);
			}

			RefreshDevice();
		}

		public void RefreshDevice()
		{
			var go = loading;
			if (go == null) return;
			if (go.activeSelf) return;
			StartCoroutine(RefreshDeviceAnimation());
		}

		private IEnumerator RefreshDeviceAnimation()
		{
			if (loading != null) loading.SetActive(true);
			yield return new WaitForSeconds(2f);
			if (loading != null) loading.SetActive(false);

			var list = selections;
			if (list == null) yield break;

			for (int i = list.Count - 1; i >= 0; i--)
			{
				var it = list[i];
				if (it == null) continue;
				if (!it.Connected)
				{
					var go = it.gameObject;
					if (go != null) Destroy(go);
					list.RemoveAt(i);
				}
			}

			var remaining = new List<DeviceItem>(list);

			var sys = system;
			var discovered = sys != null ? sys.DiscoverDevices() : null;
			if (discovered != null)
			{
				for (int i = 0; i < discovered.Count; i++)
				{
					var d = discovered[i];
					if (d == null) continue;

					DeviceItem match = null;
					for (int j = 0; j < list.Count; j++)
					{
						var it = list[j];
						if (it != null && it.detail != null && it.detail.id == d.id) { match = it; break; }
					}

					var ready = Localization.GetText("Ready");
					if (match != null)
					{
						match.Refresh(d);
						match.Status = ready;
						remaining.Remove(match);
					}
					else
					{
						AddToList(d, ready, false);
					}
				}
			}

			var offline = Localization.GetText("Offline");
			for (int i = 0; i < remaining.Count; i++)
			{
				var it = remaining[i];
				it.Status = offline;
			}
		}

		private void AddToList(DeviceDetail detail, string status, bool connected)
		{
			var item = Instantiate(deviceItemPrefab, deviceItemParent);
			if (item == null || detail == null) return;

			var icon = GetDeviceIcon(detail.type);
			item.Icon = icon;
			item.Status = status;
			if (connected) item.Connect();
			else item.Disconnect();
			item.Refresh(detail);
            item.OnRenameClick.AddListener(() =>
            {
                RenameDevice(item);
            });
			
			if (selections != null)
			{
				selections.Add(item);
			}
		}

		private Sprite GetDeviceIcon(int type)
		{
			if (deviceIcons != null && deviceIcons.Length > 0)
			{
				for (int i = 0; i < deviceIcons.Length; i++)
				{
					var di = deviceIcons[i];
					if (di != null && di.type == type) return di.icon;
				}
			}
			return unknownDeviceIcon;
		}

		public void RenameDevice(DeviceItem device)
		{
			selected = device;
			var input = deviceNameInput;
			if (input == null) return;
			var go = input.gameObject;
			if (go != null) go.SetActive(true);
			var d = selected != null ? selected.detail : null;
			if (d != null)
			{
				input.text = d.name;
				input.ActivateInputField();
			}
		}

		public void ApplyRename()
		{
			var input = deviceNameInput;
			if (input == null) return;
			var go = input.gameObject;
			if (go != null) go.SetActive(false);

			var sel = selected;
			var det = sel != null ? sel.detail : null;
			var sys = system;
			if (det == null || sys == null) return;

			var dev = sys.ConnectDevice<Device>(det.id);
			if (dev != null)
			{
				var name = input.text;
				det.name = name;
				sel.Name = name;
				dev.customName = name;
				return;
			}

			var title = Localization.GetText("Error");
			var message = Localization.GetText("Unable to connect to device");
			sys.ShowMessageBox(title, message);
		}

		public override void Close()
		{
			var os = system;
			var wl = new WirelessDeviceList(os);
			var devices = new List<DeviceDetail>();
			if (selections != null)
			{
				for (int i = 0; i < selections.Count; i++)
				{
					var it = selections[i];
					if (it != null && it.Connected && it.detail != null) devices.Add(it.detail);
				}
			}
			wl.Save(devices);
			base.Close();
		}
	}
}
