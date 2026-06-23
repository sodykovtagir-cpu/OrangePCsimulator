using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software.OS
{
	public class DevicePicker : MonoBehaviour
	{
		[SerializeField]
		private GameObject noFound;

		[SerializeField]
		private Button selectButton;

		[SerializeField]
		private ListView listView;

		private Action<Device> callback;

		private OperatingSystem os;

		private DeviceDetail[] devices;

		private void Awake()
		{
			if (listView != null) listView.SelectedIndexChanged += SelectedIndexChanged;
		}

		private void SelectedIndexChanged(int index)
		{
			selectButton.interactable = index != -1;
        }

		public void PickDevice(OperatingSystem os, Action<Device> callback, int type)
		{
			this.callback = callback;
			this.os = os;

			var go = gameObject;
			if (go != null) go.SetActive(true);
			if (noFound != null) noFound.SetActive(false);

			if (listView == null || os == null) return;

			listView.Clear();

			var src = os.ListInstalledDevices();
			DeviceDetail[] filtered;

			if (src != null && src.Count > 0)
			{
				var tmp = new List<DeviceDetail>();
				for (int i = 0; i < src.Count; i++)
				{
					var d = src[i];
					if (d != null && d.type == type) tmp.Add(d);
				}
				filtered = tmp.ToArray();
			}
			else
			{
				filtered = new DeviceDetail[0];
			}

			devices = filtered;

			if (filtered.Length == 0)
			{
				if (noFound != null) noFound.SetActive(true);
				return;
			}

			for (int i = 0; i < filtered.Length; i++)
			{
				var d = filtered[i];
				var item = new ListViewItem(d.name);
				listView.Add(item);
			}
		}

		public void Select()
		{
			if (listView == null || devices == null || os == null) return;

			int index = listView.SelectedIndex;
			if (index < 0 || index >= devices.Length) return;

			var d = devices[index];
			var x = os.ConnectDevice<Device>(d.id);

			if (x != null)
			{
				var cb = callback;
				if (cb != null) cb(x);
			}
			else
			{
				var title = Localization.GetText("Error");
				var message = Localization.GetText("Unable to connect to device");
				os.ShowMessageBox(title, message);
			}

			var go = gameObject;
			if (go != null) go.SetActive(false);
		}

		public void Cancel()
		{
			var go = gameObject;
			if (go != null) go.SetActive(false);
		}
	}
}
