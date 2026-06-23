using Newtonsoft.Json.Linq;
using UnityEngine;

namespace PC.Component
{
	[RequireComponent(typeof(Item))]
	public abstract class Device : MonoBehaviour, ISave
	{
		[SerializeField]
		private string deviceName;

		[SerializeField]
		private int deviceType;

		public string customName;

		private Item item;

		public string DeviceName => string.IsNullOrEmpty(customName) ? deviceName : customName;

		public int DeviceType => deviceType;

		public int Id => item.Id;

		protected virtual void Awake()
        {
			item = GetComponent<Item>();
        }

		public virtual void OnDeviceStart() { return; }

		public virtual void OnDeviceStop() { return; }

        public virtual void ToData(JObject jObject)
        {
            jObject["customName"] = customName;
        }

        public virtual void FromData(JObject jObject)
        {
            if (jObject.TryGetValue("customName", out var token) && token != null)
            {
                customName = token.ToString();
            }
        }
	}
}
