using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class DisplayManager : MonoBehaviour
{
	private Dictionary<Canvas, (Camera, RenderTexture)> camDict = new Dictionary<Canvas, (Camera, RenderTexture)>();
	// Размытые копии экранов: для каждой камеры/RT свой блюр (для сэмпла из UI-шейдера).
	private Dictionary<Camera, RenderTexture> blurTex = new Dictionary<Camera, RenderTexture>();

	public static DisplayManager Instance { get; private set; }

	private Material blurMat;
	// Глобальное имя текстуры блюра, которую читает шейдер стекла.
	private static readonly int ScreenBlurTexId = Shader.PropertyToID("_UIScreenBlur");
	private const int BlurScale = 8;
	private const int BlurMinSize = 64;

	private void Awake()
    {
		Instance = this;
    }

	private void OnEnable()
	{
		RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
	}

	private void OnDisable()
	{
		RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
	}

	private Material GetBlurMaterial()
	{
		if (blurMat == null)
		{
			var shader = Shader.Find("Hidden/UiScreenBlur");
			if (shader != null) blurMat = new Material(shader);
		}
		return blurMat;
	}

	private void OnEndCameraRendering(ScriptableRenderContext ctx, Camera cam)
	{
		// Готовим размытую копию экрана для наших UI-камер (мониторы).
		if (cam == null) return;
		if (!blurTex.TryGetValue(cam, out var blur) || blur == null) return;
		if (cam.targetTexture == null) return;

		var mat = GetBlurMaterial();
		if (mat == null) return;

		var src = cam.targetTexture;
		Graphics.Blit(src, blur, mat);
		Shader.SetGlobalTexture(ScreenBlurTexId, blur);
	}

	public RenderTexture CreateDisplay(Canvas canvas, int width, int height)
	{
		width = Mathf.Max(width, 1280);
		height = Mathf.Max(height, 720);
		var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
		rt.antiAliasing = 4;
		rt.filterMode = FilterMode.Bilinear;
		rt.anisoLevel = 4;
		var go = new GameObject("Display Camera");
		go.transform.position = new Vector3(500f, 0f, 0f);
		var cam = go.AddComponent<Camera>();
		cam.cullingMask = LayerMask.GetMask("UI");
		cam.targetTexture = rt;
		cam.backgroundColor = Color.black;
		cam.clearFlags = CameraClearFlags.SolidColor;
		camDict.Add(canvas, (cam, rt));
		canvas.worldCamera = cam;

		// Размытая копия экрана (сильный даунсэмпл + box blur).
		int bw = Mathf.Max(BlurMinSize, width / BlurScale);
		int bh = Mathf.Max(BlurMinSize, height / BlurScale);
		var blur = new RenderTexture(bw, bh, 0, RenderTextureFormat.ARGB32);
		blur.filterMode = FilterMode.Bilinear;
		blur.wrapMode = TextureWrapMode.Clamp;
		blurTex[cam] = blur;

		cam.Render();
		return rt;
	}

	public void SetDisplayActive(Canvas canvas, bool value)
	{
		if (camDict != null && camDict.TryGetValue(canvas, out var entry)) {
			entry.Item1.enabled = value;
			entry.Item1.Render();
			if (value)
			{
				var mat = GetBlurMaterial();
				if (mat != null && blurTex.TryGetValue(entry.Item1, out var blur) && entry.Item1.targetTexture != null)
				{
					Graphics.Blit(entry.Item1.targetTexture, blur, mat);
					Shader.SetGlobalTexture(ScreenBlurTexId, blur);
				}
			}
		}
	}

	public void RemoveDisplay(UnityEngine.Canvas canvas)
	{
		if (camDict != null && camDict.TryGetValue(canvas, out var entry))
		{
			if (blurTex.TryGetValue(entry.Item1, out var blur))
			{
				if (blur != null) blur.Release();
				blurTex.Remove(entry.Item1);
			}
			Destroy(entry.Item1);
			Destroy(entry.Item2);
			camDict.Remove(canvas);
		}
	}
}
