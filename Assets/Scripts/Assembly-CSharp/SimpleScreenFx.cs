using UnityEngine;

[RequireComponent(typeof(Camera))]
[ImageEffectAllowedInSceneView]
public class SimpleScreenFx : MonoBehaviour
{
	public bool bloom;
	public bool vignette;
	public bool chromatic;
	public bool motionBlur;

	private Material mat;
	private Camera cam;
	private Vector3 lastPos;
	private Quaternion lastRot;
	private bool hasLast;

	private void Awake()
	{
		cam = GetComponent<Camera>();
	}

	private void OnDestroy()
	{
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

		float motionAmt = 0f;
		Vector2 motionDir = Vector2.zero;
		if (motionBlur && cam != null)
		{
			if (!hasLast)
			{
				lastPos = cam.transform.position;
				lastRot = cam.transform.rotation;
				hasLast = true;
			}
			else
			{
				Vector3 dp = cam.transform.position - lastPos;
				float ang = Quaternion.Angle(lastRot, cam.transform.rotation);
				float speed = dp.magnitude * 8f + ang * 0.04f;
				if (speed > 0.015f)
				{
					motionAmt = Mathf.Clamp01(speed);
					Vector3 local = cam.transform.InverseTransformDirection(dp.sqrMagnitude > 1e-8f ? dp.normalized : cam.transform.forward);
					motionDir = new Vector2(-local.x, -local.y) * (0.012f * motionAmt);
				}
				lastPos = cam.transform.position;
				lastRot = cam.transform.rotation;
			}
		}
		else
		{
			hasLast = false;
		}

		m.SetFloat("_Motion", motionAmt);
		m.SetVector("_MotionDir", motionDir);

		Graphics.Blit(src, dest, m, 3);

		if (bloomTex != null)
			RenderTexture.ReleaseTemporary(bloomTex);
	}
}
