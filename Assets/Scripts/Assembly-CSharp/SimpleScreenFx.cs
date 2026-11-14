using UnityEngine;

[RequireComponent(typeof(Camera))]
public class SimpleScreenFx : MonoBehaviour
{
	public bool bloom;
	public bool vignette;
	public bool grain;
	public bool ao;

	private Material mat;
    private void OnDestroy() { if (mat != null) Destroy(mat); }

	private Material Mat()
	{
		if (mat == null)
		{
			// Шейдер лежит в Assets/Resources, чтобы гарантированно попасть в билд.
			// Shader.Find в билде работает только для включённых в сборку шейдеров.
			var sh = Resources.Load<Shader>("SimpleScreenFx");
			if (sh == null)
				sh = Shader.Find("Hidden/OrangePC/SimpleScreenFx");
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
		bool any = bloom || vignette || grain || ao;
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
			m.SetFloat("_Threshold", 0.82f);
			Graphics.Blit(src, rt0, m, 0);
			Graphics.Blit(rt0, rt1, m, 1);
			Graphics.Blit(rt1, rt0, m, 2);
			Graphics.Blit(rt0, rt1, m, 1);
			Graphics.Blit(rt1, rt0, m, 2);
			bloomTex = rt0;
			RenderTexture.ReleaseTemporary(rt1);
			m.SetTexture("_BloomTex", bloomTex);
			m.SetFloat("_Bloom", 0.32f);
		}
		else
		{
			m.SetFloat("_Bloom", 0f);
		}

		m.SetFloat("_Vignette", vignette ? 0.16f : 0f);
		m.SetFloat("_Grain", grain ? 0.08f : 0f);
		m.SetFloat("_AO", ao ? 0.28f : 0f);

		var composed = RenderTexture.GetTemporary(w, h, 0, fmt);
		Graphics.Blit(src, composed, m, 3);
		Graphics.Blit(composed, dest);

		RenderTexture.ReleaseTemporary(composed);
		if (bloomTex != null)
			RenderTexture.ReleaseTemporary(bloomTex);
	}

}
