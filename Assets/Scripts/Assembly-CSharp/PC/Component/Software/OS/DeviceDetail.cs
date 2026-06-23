using System;

namespace PC.Component.Software.OS
{
	[Serializable]
	public class DeviceDetail
	{
		public string name;

		public int type;

		public int id;

		public DeviceDetail(string name, int type, int id)
        {
			this.name = name;
			this.type = type;
			this.id = id;
        }
	}
}
