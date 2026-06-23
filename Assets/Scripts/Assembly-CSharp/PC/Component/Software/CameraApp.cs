using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
	public class CameraApp : App
	{
		[SerializeField]
		private RawImage display;

		[SerializeField]
		private Vector2Int resolution;

		[SerializeField]
		private GameObject pickCamera;

		[SerializeField]
		private GameObject takePicture;

		private RenderTexture rt;

		private CameraDevice camDevice;

		public void PickCamera()
		{
			var os = system;
			if (os == null) return;
			var picker = os.DevicePicker;
			if (picker == null) return;

			System.Action<Device> cb = device =>
			{
				int width = resolution.x;
				int height = resolution.y;

				var rtex = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
				rt = rtex;
				rtex.filterMode = FilterMode.Point;

				if (display != null) display.texture = rtex;
				if (display != null) display.color = Color.black;

				var camDev = device as CameraDevice;
				camDevice = camDev;

				var cam = camDev != null ? camDev.Cam : null;
				if (cam != null) cam.targetTexture = rtex;
				RenderTexture.active = rtex;
				GL.Clear(true, true, Color.black);
				RenderTexture.active = null;
				
				if (camDevice != null) camDevice.OnDeviceStart();
				if (display != null) display.color = Color.white;

				if (pickCamera != null) pickCamera.SetActive(false);
				if (takePicture != null) takePicture.SetActive(true);
			};

			picker.PickDevice(os, cb, 3);
		}

		public void TakePicture()
		{
			int width = resolution.x;
			int height = resolution.y;

			var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
			RenderTexture.active = rt;
			var rect = new Rect(0f, 0f, width, height);
			tex.ReadPixels(rect, 0, 0);
			RenderTexture.active = null;

			var data = ImageConversion.EncodeToPNG(tex);
			var content = System.Convert.ToBase64String(data);

			var os = system;
			if (os != null && os.SaveDialog != null)
			{
				var extensions = new[] { ".pic" };
				os.SaveDialog.ShowDialog("Untitled", content, extensions);
			}

			Destroy(tex);
		}

		public override void Close()
		{
			var d = camDevice;
			if (d != null) d.OnDeviceStop();
			base.Close();
		}
	}
}
