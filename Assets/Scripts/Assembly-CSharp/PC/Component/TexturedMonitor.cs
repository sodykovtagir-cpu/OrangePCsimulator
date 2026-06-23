using UnityEngine;

namespace PC.Component
{
	public class TexturedMonitor : Display
	{
		[SerializeField]
		private Renderer screenRenderer;

		[SerializeField]
		private Material screenMaterial;

		[SerializeField]
		private int screenWidth = 256;

		[SerializeField]
		private int screenHeight = 256;

		protected override void Start()
		{
			base.Start();
			if (Damaged) return;
			var dm = DisplayManager.Instance;
			if (dm == null)
			{
				Debug.LogError("TexturedMonitor required DisplayManager, please add one to the scene!");
				return;
			}
			canvas.transform.SetParent(null, false);
			var rt = dm.CreateDisplay(canvas, screenWidth, screenHeight);
			screenRenderer.material = screenMaterial;
			screenRenderer.material.mainTexture = rt;
		}

		private void OnBecameVisible()
		{
			if (Damaged) return;
			DisplayManager.Instance.SetDisplayActive(canvas, true);
		}

		private void OnBecameInvisible()
		{
			if (Damaged) return;
			if (!canvas) return;
			DisplayManager.Instance.SetDisplayActive(canvas, false);
		}

		protected override void ResetCanvas() { 
			if (!canvas) return;
			 canvas.transform.SetParent(null, false);
			  canvas.renderMode = RenderMode.ScreenSpaceCamera;
		}

		private void OnDestroy()
		{
			DisplayManager.Instance?.RemoveDisplay(canvas);
		}
	}
}
