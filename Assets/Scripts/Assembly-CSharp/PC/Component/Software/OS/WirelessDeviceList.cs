using System;
using System.Collections.Generic;

namespace PC.Component.Software.OS
{
	public class WirelessDeviceList
	{
		[Serializable]
		public class Wrapper
		{
			public List<DeviceDetail> devices;
		}

		private readonly string filePath = "System/devices";

		private OperatingSystem os;

		public WirelessDeviceList(OperatingSystem os)
        {
			this.os = os;
        }

		public List<DeviceDetail> ListAllDevices()
		{
			var system = os;
			var fm = system != null ? system.FileManager : null;
			if (fm == null) return new List<DeviceDetail>();

			if (fm.TryGetFile(0, filePath, out var file) && file != null)
			{
				var w = UnityEngine.JsonUtility.FromJson<Wrapper>(file.content);
				if (w != null && w.devices != null) return w.devices;
				return new List<DeviceDetail>();
			}

			return new List<DeviceDetail>();
		}

		public void Save(List<DeviceDetail> devices)
		{
			var w = new Wrapper { devices = devices };
			var system = os;
			var fm = system != null ? system.FileManager : null;
			var path = filePath;
			if (fm == null || path == null) return;
			var content = UnityEngine.JsonUtility.ToJson(w);
			var file = fm.Write(0, path, content);
			if (file != null) file.hidden = true;
		}
	}
}
