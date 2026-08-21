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
			prev = null;
		}
		if (mat != null)
			Destroy(mat);
	}

	private void OnRenderImage(RenderTexture src, RenderTexture dest)
	{
		bool any = bloom || vignette || chromatic || motionBlur;
		if (!any)
		{
			Graphics.Blit(src, dest);
			return;
		}

		if (mat == null)
		{
			var sh = Shader.Find("Hidden/OrangePC/SimpleScreenFx");
			if (sh == null)
			{
				Debug.LogWarning("[SimpleScreenFx] Shader Hidden/OrangePC/SimpleScreenFx not found.");
				Graphics.Blit(src, dest);
				return;
			}
			mat = new Material(sh);
		}

		mat.SetFloat("_Vignette", vignette ? 0.65f : 0f);
		mat.SetFloat("_Chromatic", chromatic ? 1f : 0f);
		mat.SetFloat("_Bloom", bloom ? 2.4f : 0f);
		mat.SetFloat("_Motion", motionBlur ? 1f : 0f);

		if (prev == null)
			mat.SetTexture("_PrevTex", src);
		else
			mat.SetTexture("_PrevTex", prev);

		Graphics.Blit(src, dest, mat);

		if (motionBlur)
		{
			if (prev == null || prev.width != src.width || prev.height != src.height)
			{
				if (prev != null) prev.Release();
				prev = new RenderTexture(src.width, src.height, 0, src.format);
				prev.Create();
			}
			Graphics.Blit(dest, prev);
		}
	}
}
