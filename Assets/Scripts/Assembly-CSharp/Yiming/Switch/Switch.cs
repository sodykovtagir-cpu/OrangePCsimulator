using UnityEngine;

namespace Yiming.Switch
{
	public abstract class Switch : MonoBehaviour
	{
		public ToggleEvent onToggle;

		[SerializeField]
		private bool isOn;

		public bool IsOn
		{
			get
			{
				return isOn;
			}
			set
			{
				isOn = value;
				onToggle.Invoke(value);
				OnToggleChanged();
			}
		}

		protected abstract void OnToggleChanged();
	}
}
