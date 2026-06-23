using System.Collections.Generic;
using UnityEngine;

public class DisplayManager : MonoBehaviour
{
	private Dictionary<Canvas, (Camera, RenderTexture)> camDict = new Dictionary<Canvas, (Camera, RenderTexture)>();

	public static DisplayManager Instance { get; private set; }

	private void Awake()
    {
		Instance = this;
    }

	public RenderTexture CreateDisplay(Canvas canvas, int width, int height)
	{
		var rt = new RenderTexture(width, height, 24, RenderTextureFormat.RGB565);
		var go = new GameObject("Display Camera");
		go.transform.position = new Vector3(500f, 0f, 0f);
		var cam = go.AddComponent<Camera>();
		cam.cullingMask = LayerMask.GetMask("UI");
		cam.targetTexture = rt;
		cam.backgroundColor = Color.black;
		cam.clearFlags = CameraClearFlags.SolidColor;
		camDict.Add(canvas, (cam, rt));
		canvas.worldCamera = cam;
		cam.Render();
		return rt;
	}

	public void SetDisplayActive(Canvas canvas, bool value)
	{
		if (camDict != null && camDict.TryGetValue(canvas, out var entry)) {
			entry.Item1.enabled = value;
		entry.Item1.Render();}
	}

	public void RemoveDisplay(UnityEngine.Canvas canvas)
	{
		if (camDict != null && camDict.TryGetValue(canvas, out var entry))
		{
			Destroy(entry.Item1);
			Destroy(entry.Item2);
			camDict.Remove(canvas);
		}
	}
}
