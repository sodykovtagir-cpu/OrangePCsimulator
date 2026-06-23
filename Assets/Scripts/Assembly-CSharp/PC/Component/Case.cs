using UnityEngine;

namespace PC.Component
{
	public class Case : MonoBehaviour
	{
		[SerializeField]
		private Renderer button;

		[SerializeField]
		private Material onMaterial;

		[SerializeField]
		private HardwareSlot motherboard;

		[SerializeField]
		private Motherboard.External external;

		private Material oldMat;

		public HardwareSlot Motherboard
		{
			get
			{
				return motherboard;
			}
			private set
            {
				motherboard = value;
            }
		}

		public Motherboard.External External
		{
			get
			{
				return external;
			}
			private set
            {
				external = value;
            }
		}

		private void Awake()
		{
			motherboard.onChanged.AddListener(MotherboardChanged);
		}

		private void Start()
        {
			oldMat = button.sharedMaterial;
        }

		private void MotherboardChanged()
		{
			if (motherboard == null)
				return;

			var mb = motherboard.Hardware as Motherboard;

			if (!motherboard.Ready)
			{
				if (mb != null)
				{
					if (mb.Running)
						mb.PowerOff(false);

					mb.RemoveExternal(external);
					mb.PowerChanged -= UpdatePower;
				}
			}
			else
			{
				if (mb != null)
				{
					mb.AddExternal(external);
					mb.PowerChanged += UpdatePower;
				}
			}
		}

		public void Switch()
		{
			if (motherboard == null)
				return;

			if (!motherboard.Ready)
			{
				var main = Main.Instance;
				if (main && !main.hardcore)
				{
					string msg = "<color=red>" + Localization.GetText("Motherboard Missing!") + "</color> ";
					main.FadeText(msg);
				}
				return;
			}

			var mb = motherboard.Hardware as Motherboard;
			if (mb != null)
			{
				mb.Switch();
			}
		}

		public void UpdatePower(bool on)
		{
			if (on) button.material = onMaterial;
			else button.material = oldMat;
		}
	}
}
