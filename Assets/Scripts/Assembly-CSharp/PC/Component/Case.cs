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

        public Motherboard ActiveBoard { get; private set; }
        public event System.Action<Motherboard> BoardChanged;

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
            if (button != null) oldMat = button.sharedMaterial;
			if (motherboard != null) motherboard.onChanged.AddListener(MotherboardChanged);
		}

		private void Start()
        {
            MotherboardChanged();
        }

        private void OnDestroy()
        {
            if (motherboard != null) motherboard.onChanged.RemoveListener(MotherboardChanged);
            DisconnectBoard(false);
        }

        private void DisconnectBoard(bool reportHosting = true)
        {
            var previous = ActiveBoard;
            ActiveBoard = null;
            BoardChanged?.Invoke(null);
            if (previous == null) return;
            previous.PowerChanged -= UpdatePower;
            if (previous.Running) previous.PowerOff(false, reportHosting);
            previous.RemoveExternal(external);
            UpdatePower(false);
        }

        private void MotherboardChanged()
        {
            var next = motherboard != null && motherboard.Ready
                ? motherboard.Hardware as Motherboard : null;
            if (next == ActiveBoard) return;
            DisconnectBoard();
            ActiveBoard = next;
            if (next == null) return;
            next.AddExternal(external);
            next.PowerChanged += UpdatePower;
            UpdatePower(next.Running);
            BoardChanged?.Invoke(next);
        }

		public void Switch()
		{
			if (motherboard == null)
				return;

			if (!motherboard.Ready)
			{
				// В консоль пишем всегда, в том числе в хардкоре: подсказка на
				// экране там запрещена, но разработчик должен видеть причину.
				// Именно этот случай выглядит как «жму кнопку, ничего не
				// происходит».
				Debug.Log("Case: плата не готова (motherboard.Ready == false), запуск отменён");

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
			if (button == null) return;
			if (on && onMaterial != null) button.material = onMaterial;
			else button.material = oldMat;
		}
	}
}
