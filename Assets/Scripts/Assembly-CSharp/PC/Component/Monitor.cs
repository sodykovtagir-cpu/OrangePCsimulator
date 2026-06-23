using UnityEngine;

namespace PC.Component
{
	public class Monitor : Display
	{
		[SerializeField]
		private Transform point;

		protected override void ResetCanvas()
		{
			canvas.transform.SetParent(point);
			canvas.renderMode = RenderMode.WorldSpace;
		}
	}
}
