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
    private Camera viewCamera;
    private bool havePrevious;
    private bool haveVelocity;
    private Vector4 velocityRate;
    private Vector3 previousPosition;
    private Quaternion previousRotation;
    private float previousFov;
    private int previousWidth, previousHeight, previousFrame = -1;

    private void OnEnable() { ResetMotion(); }
    private void OnDisable() { ResetMotion(); }
    private void ResetMotion() { havePrevious = false; previousFrame = -1; haveVelocity = false; velocityRate = new Vector4(); }
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

        ConfigureMotion(m, src);

		var composed = RenderTexture.GetTemporary(w, h, 0, fmt);
		Graphics.Blit(src, composed, m, 3);
		Graphics.Blit(composed, dest);

		RenderTexture.ReleaseTemporary(composed);
		if (bloomTex != null)
			RenderTexture.ReleaseTemporary(bloomTex);
	}

    private void ConfigureMotion(Material material, RenderTexture source)
    {
        material.SetFloat("_Motion", 0);
        if (viewCamera == null) viewCamera = GetComponent<Camera>();
        if (!motionBlur || viewCamera == null || Time.timeScale <= 0f)
        { ResetMotion(); return; }
        // Save thumbnails call Camera.Render again into an RT. They must not
        // advance temporal state or contaminate the next screen frame.
        if (viewCamera.targetTexture != null || previousFrame == Time.frameCount) return;
        Vector3 position = viewCamera.transform.position;
        Quaternion rotation = viewCamera.transform.rotation;
        bool cut = !havePrevious || previousFrame != Time.frameCount - 1 ||
            Vector3.Distance(position, previousPosition) > 3f || Quaternion.Angle(rotation, previousRotation) > 45f ||
            Mathf.Abs(previousFov-viewCamera.fieldOfView) > .5f || previousWidth != source.width || previousHeight != source.height || Time.unscaledDeltaTime > .1f;
        Vector3 local = new Vector3(); float yaw = 0, pitch = 0;
        if (!cut)
        {
            local = viewCamera.transform.InverseTransformDirection(position-previousPosition);
            Quaternion delta = Quaternion.Inverse(previousRotation)*rotation;
            // Do not round-trip tiny render-frame deltas through Euler
            // decomposition. atan2 remains sensitive near identity at high FPS.
            yaw = 2f * Mathf.Atan2(delta.y,delta.w) * Mathf.Rad2Deg;
            pitch = 2f * Mathf.Atan2(delta.x,delta.w) * Mathf.Rad2Deg;
            yaw = Mathf.DeltaAngle(0,yaw); pitch = Mathf.DeltaAngle(0,pitch);
        }
        previousPosition=position; previousRotation=rotation; previousFov=viewCamera.fieldOfView;
        previousWidth=source.width; previousHeight=source.height; previousFrame=Time.frameCount; havePrevious=true;
        if (cut) { haveVelocity = false; velocityRate = new Vector4(); return; }
        // Use a 1/40-second exposure, not a fraction that gets imperceptible
        // on high-refresh screens. Vectors are frame deltas, so normalize by dt.
        // No old colour buffer/history is accumulated.
        material.SetFloat("_Motion", 1);
        float fov = Mathf.Max(1,viewCamera.fieldOfView), aspect = Mathf.Max(.1f,viewCamera.aspect);
        float dt = Mathf.Max(.001f,Time.unscaledDeltaTime);
        var rate = new Vector4((-yaw/(fov*aspect)-local.x*.14f)/dt,
            (pitch/fov+local.y*.14f)/dt, local.z*.14f/dt, 0);
        // Input polling and render frames are not synchronized. Smooth velocity
        // over 15 ms, not colour frames: no doubled/accumulated image history.
        float blend = haveVelocity ? 1f-Mathf.Exp(-dt/.015f) : 1f;
        velocityRate = new Vector4(velocityRate.x+(rate.x-velocityRate.x)*blend,
            velocityRate.y+(rate.y-velocityRate.y)*blend,
            velocityRate.z+(rate.z-velocityRate.z)*blend,0);
        float magnitude = Mathf.Abs(velocityRate.x)+Mathf.Abs(velocityRate.y)+Mathf.Abs(velocityRate.z);
        haveVelocity = magnitude > .0001f;
        if (!haveVelocity) velocityRate = new Vector4();
        material.SetVector("_FallbackMotion", new Vector4(velocityRate.x*dt,velocityRate.y*dt,velocityRate.z*dt,0));
        // Never switch between unrelated camera/native directions per pixel
        // while the camera moves: that threshold used to make the blur jump.
        // Native vectors still cover moving objects with a stationary camera.
        material.SetFloat("_UseMotionVectors", SystemInfo.supportsMotionVectors && !haveVelocity ? 1 : 0);
        material.SetFloat("_Shutter", Mathf.Clamp((1f/40f)/Mathf.Max(.001f,Time.unscaledDeltaTime),.25f,6f));
        material.SetFloat("_MaxMotionPixels", 24f);
    }
}
