using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using PC.Component.Software.OS;
using UnityEngine;

namespace PC.Component
{
	public class Motherboard : Hardware
	{
		[Serializable]
		public class External
		{
			public HardwareSlot[] CPU;

			public HardwareSlot[] CPU_fan;

			public HardwareSlot[] GPU;

			public HardwareSlot[] RAM;

			public HardwareSlot[] drive;

			public HardwareSlot[] output;

			public HardwareSlot[] supply;

			public HardwareSlot[] usb;
		}

		[SerializeField]
		private AudioClip beepSound;

		[SerializeField]
		private Bios biosPrefab;

		public External external;

		[HideInInspector]
		public Display monitor;

		public Bios.BiosSettings BiosSettings;

		private List<External> externals = new List<External>();

		private bool running;

		[SerializeField]
		[Header("Devices")]
		private float wirelessRange;

		public AudioSource Source { get; private set; }

		public bool Running => running;

		public ComputerSystem System { get; private set; }

		public float WirelessRange => wirelessRange;

		private void Awake()
        {
			Source = GetComponent<AudioSource>();
			AddExternal(external);
        }

		public void Switch()
        {
			if (running) {
				System.PowerClicked();
				return;
			}
			if (Main.Instance && !Main.Instance.hardcore)
				Main.Instance.FadeText(Boot());
        }

		public string Boot()
		{
			if (!Done(out var message))
				return message;

			var hardwares = GetAllHardwares(false);
			var supplies  = GetHardwares(HardwareType.Supply);

			float totalSupplyW = 0f;
			if (supplies != null)
			{
				foreach (var obj in supplies)
				{
					var s = obj as Supply;
					totalSupplyW += s.Wattage;
				}
			}

			float requiredW = GetWattage(hardwares);
			Debug.Log("Wattage: " + requiredW);

			if (requiredW <= totalSupplyW)
			{
				BootSystem();
				return message;
			}

			if (supplies != null)
			{
				foreach (var obj in supplies)
				{
					var s = obj as Supply;
					s.Overload();
				}
			}

			return "<color=red>"
				+ Localization.GetText("Supply Overload!")
				+ " ("
				+ requiredW.ToString()
				+ "W)</color>";
		}

		public void SwitchSystem(int storage, ComputerSystem cs)
		{
			if (System != null)
			{
				Destroy(System.gameObject);
			}

			var parent = transform;
			var instance = Instantiate(cs, parent);
			System = instance;

			instance.Init(this, storage);

			if (monitor != null)
			{
				var display = instance.GetComponent<RectTransform>();
				monitor.ApplyScreen(display);
			}
		}

		private void BootSystem()
		{
			running = true;

			var allSlots = GetAllSlots();
			foreach (var obj in allSlots)
			{
				var slot = obj as HardwareSlot;
				slot.Switch(true);
			}

			Switch(true, false);

			if (Source != null)
			{
				Source.PlayOneShot(beepSound);

				SwitchSystem(-1, biosPrefab);

				if (monitor != null)
				{
					monitor.Switch(true, false);
					monitor.AllowZoom(true);
				}
				var main = Main.Instance;
				if (main != null && !main.example)
				{
					if (main.playTime < 60f)
					{
						CloudOnceManager.Instance.GetAchievementFromId("lightspeed")?.Unlock(null);
					}

/*					var lb = CloudOnce.Leaderboards.s_leaderboard_fastest_build;
					if (lb != null)
					{
						float ms = main.playTime * 1000f;
						long score = long.MinValue;
						if (!float.IsInfinity(ms))
							score = (long)ms;

						lb.SubmitScore(score, null);
					}*/
				}

				return;
			}
		}

		private void Fault()
        {
			if (!running) return;
			System.Fault();
        }

		public void ForceDown()
        {
			if (!running) return;
			PowerOff(false); 
        }

		private void AddHardware(Hardware hardware)
        {
			if (!running) return;
			System.AddHardware(hardware);
        }

		private void RemoveHardware(Hardware hardware)
		{
			if (!running) return;
			System.RemoveHardware(hardware);
		}

		public void PowerOff(bool restart = false)
        {
			running = false;
			Switch(false, false);

			var allSlots = GetAllSlots();
			if (allSlots != null)
			{
				foreach (var obj in allSlots)
				{
					var slot = obj as HardwareSlot;
					if (slot != null)
						slot.Switch(false);
				}
			}

			if (monitor)
			{
				monitor.AllowZoom(false);
				monitor.Switch(false, false);
			}

			if (System != null)
			{
				Destroy(System.gameObject);
			}

			if (restart)
			{
				Invoke("Boot", 1f);
			}
        }

		private bool Done(out string info)
		{
			info = "";

			var processors = GetHardwares(HardwareType.CPU);
			int cpuCount = processors != null ? processors.Count : 0;
			bool ok = true;
			if (cpuCount == 0)
			{
				info += "<color=red>" + Localization.GetText("Processor Missing!") + "</color> \n";
				ok = false;
			}

			var rams = GetHardwares(HardwareType.RAM);
			if (rams != null && rams.Count == 0)
			{
				info += "<color=red>" + Localization.GetText("Ram Missing!") + "</color> \n";
				ok = false;
			}

			var supplies = GetHardwares(HardwareType.Supply);
			if (supplies != null && supplies.Count == 0)
			{
				info += "<color=red>" + Localization.GetText("Supply Missing!") + "</color> \n";
				ok = false;
			}

			var coolers = GetHardwares(HardwareType.Cooler);
			if (coolers != null && coolers.Count < cpuCount)
			{
				info += "<color=Orange>" + Localization.GetText("Cooler Missing!") + "</color> \n";
			}

			return ok;
		}

		public void AddExternal(Motherboard.External e)
		{
			if (e == null) return;

			if (externals != null)
				externals.Add(e);

			if (e.CPU != null)
			{
				foreach (var s in e.CPU)
					if (s != null && s.onChanged != null)
						s.onChanged.AddListener(ForceDown);
			}

			if (e.GPU != null)
			{
				foreach (var s in e.GPU)
					if (s != null && s.onChanged != null)
						s.onChanged.AddListener(Fault);
			}

			if (e.RAM != null)
			{
				foreach (var s in e.RAM)
					if (s != null && s.onChanged != null)
						s.onChanged.AddListener(Fault);
			}

			if (e.supply != null)
			{
				foreach (var s in e.supply)
					if (s != null && s.onChanged != null)
						s.onChanged.AddListener(ForceDown);
			}

			if (e.drive != null)
			{
				foreach (var s in e.drive)
				{
					if (s == null) continue;
					s.HardwareConnected += AddHardware;
					s.HardwareDisconnected += RemoveHardware;
				}
			}

			if (e.usb != null)
			{
				foreach (var s in e.usb)
				{
					if (s == null) continue;
					s.HardwareConnected += AddHardware;
					s.HardwareDisconnected += RemoveHardware;
				}
			}
		}

		public void RemoveExternal(External e)
		{
			if (e == null) return;

			if (externals != null)
				externals.Remove(e);

			if (e.CPU != null)
			{
				foreach (var s in e.CPU)
					if (s != null && s.onChanged != null)
						s.onChanged.RemoveListener(ForceDown);
			}

			if (e.GPU != null)
			{
				foreach (var s in e.GPU)
					if (s != null && s.onChanged != null)
						s.onChanged.RemoveListener(Fault);
			}

			if (e.RAM != null)
			{
				foreach (var s in e.RAM)
					if (s != null && s.onChanged != null)
						s.onChanged.RemoveListener(Fault);
			}

			if (e.supply != null)
			{
				foreach (var s in e.supply)
					if (s != null && s.onChanged != null)
						s.onChanged.RemoveListener(ForceDown);
			}

			if (e.drive != null)
			{
				foreach (var s in e.drive)
				{
					if (s == null) continue;
					s.HardwareConnected -= AddHardware;
					s.HardwareDisconnected -= RemoveHardware;
				}
			}

			if (e.usb != null)
			{
				foreach (var s in e.usb)
				{
					if (s == null) continue;
					s.HardwareConnected -= AddHardware;
					s.HardwareDisconnected -= RemoveHardware;
				}
			}
		}

		public void ConnectMonitor(Display m)
		{
			if (m == null) return;
			if (m.Damaged) return;

			if (monitor != m)
			{
				if (monitor)
				{
					monitor.DisconnectBoard();
				}

				m.ConnectBoard(this);

				if (System)
				{
					var rt = System.GetComponent<RectTransform>();
					m.ApplyScreen(rt);
				}

				m.AllowZoom(running);

				monitor = m;
			}
		}

		public void ResetDisplay()
        {
			if (System)
			{
				System.transform.SetParent(transform);
				System.transform.localPosition = Vector3.zero;
			}
			monitor = null;
        }

		private float GetWattage(List<Hardware> hardwares)
		{
			if (hardwares == null) return 0f;

			float total = 0f;
			foreach (var h in hardwares)
			{
				if (h == null) continue;
				total += h.Wattage;
			}
			return total;
		}

		private List<HardwareSlot> GetAllSlots()
		{
			var list = new List<HardwareSlot>();

			var s0 = GetSlots(HardwareType.CPU);
			if (s0 != null) list.AddRange(s0);

			var s1 = GetSlots(HardwareType.Cooler);
			if (s1 != null) list.AddRange(s1);

			var s2 = GetSlots(HardwareType.GPU);
			if (s2 != null) list.AddRange(s2);

			var s3 = GetSlots(HardwareType.RAM);
			if (s3 != null) list.AddRange(s3);

			var s4 = GetSlots(HardwareType.Drive);
			if (s4 != null) list.AddRange(s4);

			var s6 = GetSlots(HardwareType.Supply);
			if (s6 != null) list.AddRange(s6);

			var s5 = GetSlots(HardwareType.Output);
			if (s5 != null) list.AddRange(s5);

			return list;
		}

		private List<Hardware> GetAllHardwares(bool includeSupply)
		{
			var list = new List<Hardware>();

			var h0 = GetHardwares(HardwareType.CPU);    if (h0 != null) list.AddRange(h0);
			var h1 = GetHardwares(HardwareType.Cooler); if (h1 != null) list.AddRange(h1);
			var h2 = GetHardwares(HardwareType.GPU);    if (h2 != null) list.AddRange(h2);
			var h3 = GetHardwares(HardwareType.RAM);    if (h3 != null) list.AddRange(h3);
			var h4 = GetHardwares(HardwareType.Drive);  if (h4 != null) list.AddRange(h4);
			var h6 = GetHardwares(HardwareType.Output); if (h6 != null) list.AddRange(h6);

			if (includeSupply)
			{
				var h5 = GetHardwares(HardwareType.Supply);
				if (h5 != null) list.AddRange(h5);
			}

			return list;
		}

		public List<Hardware> GetHardwares(HardwareType type)
		{
			var result = new List<Hardware>();

			var slots = GetSlots(type);
			if (slots == null) return result;

			foreach (var slot in slots)
			{
				if (slot?.Hardware != null)
					result.Add(slot.Hardware);
			}

			return result;
		}

		public List<HardwareSlot> GetSlots(HardwareType type)
		{
			var slots = new List<HardwareSlot>();
			if (externals == null) return slots;

			foreach (var ext in externals)
			{
				if (ext == null) continue;

				switch (type)
				{
					case HardwareType.CPU:
						if (ext.CPU != null) foreach (var s in ext.CPU) if (s) slots.Add(s);
						break;

					case HardwareType.Cooler:
						if (ext.CPU_fan != null) foreach (var s in ext.CPU_fan) if (s) slots.Add(s);
						break;

					case HardwareType.GPU:
						if (ext.GPU != null) foreach (var s in ext.GPU) if (s) slots.Add(s);
						break;

					case HardwareType.RAM:
						if (ext.RAM != null) foreach (var s in ext.RAM) if (s) slots.Add(s);
						break;

					case HardwareType.Drive:
						if (ext.drive  != null) foreach (var s in ext.drive)  if (s) slots.Add(s);
						if (ext.usb    != null) foreach (var s in ext.usb)    if (s) slots.Add(s);
						break;

					case HardwareType.Supply:
						if (ext.supply != null) foreach (var s in ext.supply) if (s) slots.Add(s);
						break;

					case HardwareType.Output:
						if (ext.output != null) { foreach (var s in ext.output) if (s) slots.Add(s); }
						else if (ext.usb != null) { foreach (var s in ext.usb) if (s) slots.Add(s); }
						break;
				}
			}

			return slots;
		}

		public List<Device> FindWirelessDevices()
		{
			var list = new List<Device>();
			var t = transform;
			if (t == null) return list;

			var all = FindObjectsOfType<Device>();
			if (all == null) return list;

			float rangeSqr = wirelessRange * wirelessRange;
			Vector3 origin = t.position;

			foreach (var d in all)
			{
				if (d == null) continue;
				var dt = d.transform;
				if (dt == null) continue;

				if ((dt.position - origin).sqrMagnitude < rangeSqr)
					list.Add(d);
			}

			return list;
		}

		public T ConnectDevice<T>(int id) where T : Device
		{
			var main = Main.Instance;
			if (!main) return null;

			var item = main.GetItemById(id);
			if (!item) return null;

			if (!item.TryGetComponent<T>(out var device))
				return null;

			var it = item.transform;
			var mt = transform;
			if (it == null || mt == null) return null;

			float rangeSqr = wirelessRange * wirelessRange;
			if ((it.position - mt.position).sqrMagnitude >= rangeSqr)
				return null;

			return device;
		}

		private void OnDrawGizmosSelected()
		{
			var t = transform;
			if (t == null) return;

			Gizmos.DrawWireSphere(t.position, wirelessRange);
		}

		public override void ToData(JObject jObject)
		{
			if (jObject == null) return;

			int monitorId = 0;
			if (monitor)
			{
				monitorId = monitor.Id;
			}
			jObject["monitorId"] = monitorId;
			jObject["bios"] = JToken.FromObject(BiosSettings);

			base.ToData(jObject);
		}

		public override void FromData(JObject jObject)
		{
			if (jObject == null) return;

			int id = jObject.Value<int>("monitorId");

			if (jObject.TryGetValue("bios", out var biosTok) && biosTok != null)
			{
				BiosSettings = biosTok.ToObject<Bios.BiosSettings>();
			}

			if (id != 0)
			{
				var main = Main.Instance;
				if (main)
				{
					var disp = main.GetItemById(id) as Display;
					if (disp) ConnectMonitor(disp);
				}
			}

			base.FromData(jObject);
		}
	}
}
