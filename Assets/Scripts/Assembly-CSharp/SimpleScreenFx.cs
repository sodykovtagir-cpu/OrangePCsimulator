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
		ReleasePrev();
		if (mat != null) Destroy(mat);
	}

	private void ReleasePrev()
	{
		if (prev == null) return;
		prev.Release();
		Destroy(prev);
		prev = null;
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
		if (m == null || !any || src == null)
		{
			Graphics.Blit(src, dest);
			return;
		}

		int w = src.width;
		int h = src.height;
		var fmt = src.format;

		RenderTexture bloomTex = null;
		if (bloom)
		{
			int bw = Mathf.Max(8, w / 4);
			int bh = Mathf.Max(8, h / 4);
			var rt0 = RenderTexture.GetTemporary(bw, bh, 0, fmt);
			var rt1 = RenderTexture.GetTemporary(bw, bh, 0, fmt);
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

		if (prev != null && prev.IsCreated())
			m.SetTexture("_PrevTex", prev);
		else
			m.SetTexture("_PrevTex", src);

		var composed = RenderTexture.GetTemporary(w, h, 0, fmt);
		Graphics.Blit(src, composed, m, 3);
		Graphics.Blit(composed, dest);

		if (motionBlur)
		{
			if (prev == null || !prev.IsCreated() || prev.width != w || prev.height != h)
			{
				ReleasePrev();
				prev = new RenderTexture(w, h, 0, fmt);
				prev.Create();
			}
			Graphics.Blit(composed, prev);
		}

		RenderTexture.ReleaseTemporary(composed);
		if (bloomTex != null)
			RenderTexture.ReleaseTemporary(bloomTex);
	}
}
