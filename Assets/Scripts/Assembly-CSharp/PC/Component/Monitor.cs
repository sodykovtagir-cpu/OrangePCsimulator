using UnityEngine;

namespace PC.Component
{
	public class Monitor : Display
	{
		[SerializeField]
		private Transform point;

		protected override void ResetCanvas()
		{
			if (canvas == null || point == null) return;
			canvas.transform.SetParent(point);
			canvas.renderMode = RenderMode.WorldSpace;
		}
	}
}
