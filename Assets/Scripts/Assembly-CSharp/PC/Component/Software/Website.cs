using PC.Component.Software.OS;
using UnityEngine;

namespace PC.Component.Software
{
	public class Website : MonoBehaviour
	{
		public Sprite icon;

		public string websiteName;

		protected OperatingSystem os;

		public void Init(OperatingSystem os)
        {
			this.os = os;
        }
	}
}
