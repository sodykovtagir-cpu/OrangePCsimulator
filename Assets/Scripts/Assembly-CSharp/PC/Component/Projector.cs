using UnityEngine;

namespace PC.Component
{
	public class Projector : Display
	{
		[SerializeField]
		private UnityEngine.Projector projector;

		[SerializeField]
		private int screenWidth = 256;

		[SerializeField]
		private int screenHeight = 256;

		protected override void Start()
		{
			base.Start();
			var manager = DisplayManager.Instance;
			if (manager == null) {
				Debug.LogError("TexturedMonitor required DisplayManager, please add one to the scene!");
				return;
			}
			var c = canvas; c.transform.SetParent(null);
			var tex = manager.CreateDisplay(c, screenWidth, screenHeight);
			var proj = projector;
			var source = proj.material;
			var mat = new Material(source);
			mat.SetTexture("_ShadowTex", tex);
			projector.material = mat;
		}

		protected override void ResetCanvas()
        {
			if (canvas != null)
				canvas.renderMode = RenderMode.ScreenSpaceCamera;
        }

		private void OnDestroy()
        {
			if (DisplayManager.Instance != null) 
				DisplayManager.Instance.RemoveDisplay(canvas);
        }
	}
}
