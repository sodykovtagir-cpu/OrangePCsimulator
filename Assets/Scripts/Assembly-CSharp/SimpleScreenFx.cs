using UnityEngine;

[RequireComponent(typeof(Camera))]
public class SimpleScreenFx : MonoBehaviour
{
	public bool bloom;
	public bool vignette;
	public bool grain;
	public bool motionBlur;
	public bool ao;

	private Material mat;
	private RenderTexture prev;
	private bool havePrevCam;
	private Vector3 prevCamPos;
	private Quaternion prevCamRot;
	private Vector2 smoothDir;
	private float smoothAmt;

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
		bool any = bloom || vignette || grain || motionBlur || ao;
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

		Vector2 motionDir;
		float motionAmt;
		ComputeCameraMotion(out motionDir, out motionAmt);
		if (!motionBlur)
		{
			motionAmt = 0f;
			havePrevCam = false;
			smoothDir = Vector2.zero;
			smoothAmt = 0f;
			ReleasePrev();
		}
		m.SetFloat("_Motion", motionAmt);
		m.SetVector("_MotionDir", new Vector4(motionDir.x, motionDir.y, 0f, 0f));
		m.SetTexture("_PrevTex", src);

		var composed = RenderTexture.GetTemporary(w, h, 0, fmt);
		Graphics.Blit(src, composed, m, 3);
		Graphics.Blit(composed, dest);

		RenderTexture.ReleaseTemporary(composed);
		if (bloomTex != null)
			RenderTexture.ReleaseTemporary(bloomTex);
	}

	private void ComputeCameraMotion(out Vector2 dirUv, out float amount)
	{
		dirUv = Vector2.zero;
		amount = 0f;
		var cam = GetComponent<Camera>();
		if (cam == null) return;

		Vector3 pos = cam.transform.position;
		Quaternion rot = cam.transform.rotation;
		if (!havePrevCam)
		{
			havePrevCam = true;
			prevCamPos = pos;
			prevCamRot = rot;
			return;
		}

		float dt = Mathf.Clamp(Time.unscaledDeltaTime, 0.0001f, 0.08f);
		Quaternion dRot = rot * Quaternion.Inverse(prevCamRot);
		Vector3 euler = dRot.eulerAngles;
		float yaw = Mathf.DeltaAngle(0f, euler.y);
		float pitch = Mathf.DeltaAngle(0f, euler.x);

		float fov = Mathf.Max(1f, cam.fieldOfView);
		float aspect = Mathf.Max(0.1f, cam.aspect);
		// Выдержка ~1/40: смаз виден при повороте/ходьбе, без длинного призрака.
		float shutter = 1f / 40f;
		float yawUv = (yaw / (fov * aspect)) * (shutter / dt);
		float pitchUv = (pitch / fov) * (shutter / dt);

		Vector3 local = cam.transform.InverseTransformDirection(pos - prevCamPos);
		float z = Mathf.Max(1.5f, Mathf.Abs(local.z) + 1.5f);
		float transX = (local.x / z) * (shutter / dt) * 0.42f;
		float transY = (local.y / z) * (shutter / dt) * 0.42f;

		Vector2 vel = new Vector2(-(yawUv + transX), pitchUv + transY);
		float mag = vel.magnitude;
		prevCamPos = pos;
		prevCamRot = rot;

		Vector2 targetDir = Vector2.zero;
		float targetAmt = 0f;
		if (mag >= 0.0003f)
		{
			targetDir = vel.normalized * Mathf.Clamp(mag, 0f, 0.042f);
			targetAmt = Mathf.Clamp01((mag - 0.0003f) * 14f) * 0.9f;
		}

		float k = mag >= 0.0003f ? 0.45f : 0.55f;
		smoothDir = Vector2.Lerp(smoothDir, targetDir, k);
		smoothAmt = Mathf.Lerp(smoothAmt, targetAmt, k);
		if (smoothAmt < 0.012f)
		{
			smoothAmt = 0f;
			smoothDir = Vector2.zero;
			return;
		}

		dirUv = smoothDir;
		amount = smoothAmt;
	}
}
