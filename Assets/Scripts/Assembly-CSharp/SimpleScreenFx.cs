using UnityEngine;

[RequireComponent(typeof(Camera))]
public class SimpleScreenFx : MonoBehaviour
{
	public bool bloom;
	public bool vignette;
	public bool chromatic;
	public bool motionBlur;

	private Material mat;
	private RenderTexture prev;

	private void OnDestroy()
	{
		if (prev != null)
		{
			prev.Release();
			Destroy(prev);
			prev = null;
		}
		if (mat != null) Destroy(mat);
	}

	private Material Mat()
	{
		if (mat == null)
		{
			var sh = Shader.Find("Hidden/OrangePC/SimpleScreenFx");
			if (sh == null)
			{
				Debug.LogWarning("[SimpleScreenFx] shader missing");
				return null;
			}
			mat = new Material(sh);
		}
		return mat;
	}

	private void OnRenderImage(RenderTexture src, RenderTexture dest)
	{
		var m = Mat();
		bool any = bloom || vignette || chromatic || motionBlur;
		if (m == null || !any)
		{
			Graphics.Blit(src, dest);
			return;
		}

		RenderTexture bloomTex = null;
		if (bloom)
		{
			int w = Mathf.Max(8, src.width / 4);
			int h = Mathf.Max(8, src.height / 4);
			var rt0 = RenderTexture.GetTemporary(w, h, 0, src.format);
			var rt1 = RenderTexture.GetTemporary(w, h, 0, src.format);
			m.SetFloat("_Threshold", 0.72f);
			Graphics.Blit(src, rt0, m, 0);
			Graphics.Blit(rt0, rt1, m, 1);
			Graphics.Blit(rt1, rt0, m, 2);
			Graphics.Blit(rt0, rt1, m, 1);
			Graphics.Blit(rt1, rt0, m, 2);
			bloomTex = rt0;
			RenderTexture.ReleaseTemporary(rt1);
			m.SetTexture("_BloomTex", bloomTex);
			m.SetFloat("_Bloom", 0.85f);
		}
		else
		{
			m.SetFloat("_Bloom", 0f);
		}

		m.SetFloat("_Vignette", vignette ? 0.38f : 0f);
		m.SetFloat("_Chromatic", chromatic ? 0.55f : 0f);
		m.SetFloat("_Motion", motionBlur ? 1f : 0f);

		if (prev != null)
			m.SetTexture("_PrevTex", prev);
		else
			m.SetTexture("_PrevTex", src);

		Graphics.Blit(src, dest, m, 3);

		if (motionBlur)
		{
			if (prev == null || prev.width != dest.width || prev.height != dest.height)
			{
				if (prev != null)
				{
					prev.Release();
					Destroy(prev);
				}
				prev = new RenderTexture(dest.width, dest.height, 0, src.format);
				prev.Create();
			}
			Graphics.Blit(dest, prev);
		}

		if (bloomTex != null)
			RenderTexture.ReleaseTemporary(bloomTex);
	}
}
