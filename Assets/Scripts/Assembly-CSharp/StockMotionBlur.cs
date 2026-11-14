using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

// Must prepare the profile before PostProcessLayer.OnPreCull builds its commands.
[DefaultExecutionOrder(-10000)]
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public sealed class StockMotionBlur : MonoBehaviour
{
    private const string ResourceName = "OrangePCPostProcessResources";
    // An unnamed, otherwise unused layer: the volume has no renderers/colliders.
    private const int VolumeLayer = 31;
    private static PostProcessResources resources;
    private static PostProcessProfile profile;
    private static PostProcessVolume volume;
    private static MotionBlur settings;
    private static int users;

    private Camera viewCamera;
    private PostProcessLayer layer;
    private bool ready, warned, havePrevious;
    private Vector3 previousPosition;
    private Quaternion previousRotation;
    private float previousFov;
    private int previousFrame = -1, previousWidth, previousHeight;

    public static bool Configure(Camera camera, bool requested)
    {
        var controller = camera.GetComponent<StockMotionBlur>();
        if (controller == null && requested)
            controller = camera.gameObject.AddComponent<StockMotionBlur>();
        if (controller == null) return false;
        if (!requested) { controller.enabled = false; return false; }
        controller.enabled = true;
        controller.EnsureReady();
        return controller.ready;
    }

    private void OnEnable()
    {
        havePrevious = false;
        EnsureReady();
        if (ready) { layer.enabled = true; layer.ResetHistory(); }
    }

    private void EnsureReady()
    {
        if (ready) return;
        if (viewCamera == null) viewCamera = GetComponent<Camera>();
        if (viewCamera == null || viewCamera.targetTexture != null) return;
        if (!SystemInfo.supportsMotionVectors || !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGHalf))
        {
            Warn("Motion vectors or RGHalf are unsupported; Motion Blur is disabled (no fake blur fallback).");
            return;
        }
        if (resources == null) resources = Resources.Load<PostProcessResources>(ResourceName);
        if (resources == null || resources.shaders.motionBlur == null || !resources.shaders.motionBlur.isSupported)
        {
            Warn("Post-processing resources / Motion Blur shader are unavailable; Motion Blur is disabled.");
            return;
        }

        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<PostProcessProfile>();
            profile.hideFlags = HideFlags.HideAndDontSave;
            settings = profile.AddSettings<MotionBlur>();
            settings.enabled.Override(true);
            settings.shutterAngle.Override(180f);
            settings.sampleCount.Override(16);
            var holder = new GameObject("Motion Blur (Unity Post-processing)");
            holder.hideFlags = HideFlags.HideAndDontSave;
            holder.layer = VolumeLayer;
            DontDestroyOnLoad(holder);
            volume = holder.AddComponent<PostProcessVolume>();
            volume.isGlobal = true;
            volume.priority = 1000f;
            volume.weight = 1f;
            volume.sharedProfile = profile;
        }

        layer = GetComponent<PostProcessLayer>();
        if (layer == null) layer = gameObject.AddComponent<PostProcessLayer>();
        layer.Init(resources);
        layer.volumeLayer = 1 << VolumeLayer;
        layer.volumeTrigger = transform;
        layer.antialiasingMode = PostProcessLayer.Antialiasing.None;
        layer.fog.enabled = false;
        // Keep the normal image-effect chain: SimpleScreenFx may run afterwards.
        layer.finalBlitToCameraTarget = false;
        layer.enabled = true;
        viewCamera.depthTextureMode |= DepthTextureMode.Depth | DepthTextureMode.MotionVectors;
        ready = true;
        users++;
        layer.ResetHistory();
    }

    private void OnPreCull()
    {
        EnsureReady();
        if (!ready) return;
        // All managed cameras use one profile. Set it immediately before THIS
        // camera's stock layer gathers volumes, rather than changing it in Update.
        // In-game monitor cameras never receive a layer in GraphicsBootstrap.
        bool suspended = viewCamera.targetTexture != null || Time.timeScale <= 0f ||
            viewCamera.pixelWidth < 32 || viewCamera.pixelHeight < 32;
        Vector3 position = viewCamera.transform.position;
        Quaternion rotation = viewCamera.transform.rotation;
        bool cut = !havePrevious || previousFrame != Time.frameCount - 1 ||
            Vector3.Distance(position, previousPosition) > 3f ||
            Quaternion.Angle(rotation, previousRotation) > 45f ||
            Mathf.Abs(previousFov - viewCamera.fieldOfView) > .5f ||
            previousWidth != viewCamera.pixelWidth || previousHeight != viewCamera.pixelHeight ||
            Time.unscaledDeltaTime > .1f;
        settings.enabled.Override(!suspended && !cut);
        if (suspended || cut) layer.ResetHistory();
        havePrevious = !suspended;
        previousPosition = position;
        previousRotation = rotation;
        previousFov = viewCamera.fieldOfView;
        previousWidth = viewCamera.pixelWidth;
        previousHeight = viewCamera.pixelHeight;
        previousFrame = Time.frameCount;
    }

    private void OnDisable()
    {
        havePrevious = false;
        if (layer != null) layer.enabled = false;
    }

    private void OnDestroy()
    {
        if (!ready || --users != 0) return;
        if (volume != null)
        {
            volume.enabled = false;
            Destroy(volume.gameObject);
        }
        if (settings != null) Destroy(settings);
        if (profile != null) Destroy(profile);
        volume = null;
        settings = null;
        profile = null;
    }

    private void Warn(string message)
    {
        if (warned) return;
        warned = true;
        Debug.LogWarning("[StockMotionBlur] " + message);
    }
}
