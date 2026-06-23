using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
public class ReplacementShaderEffect : MonoBehaviour
{
    public Shader ReplacementShader;
    public string Tag = "RenderType";
    public Color OverDrawColor = Color.red;

    private Camera _camera;
    private static readonly int OverDrawColorID = Shader.PropertyToID("_OverDrawColor");

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        Shader.SetGlobalColor(OverDrawColorID, OverDrawColor);
    }

    private void OnValidate()
    {
        Shader.SetGlobalColor(OverDrawColorID, OverDrawColor);
    }

    private void OnEnable()
    {
        if (_camera == null)
            _camera = GetComponent<Camera>();
        if (ReplacementShader == null)
            return;
        _camera.SetReplacementShader(ReplacementShader, Tag);
        Shader.SetGlobalColor(OverDrawColorID, OverDrawColor);
    }

    private void OnDisable()
    {
        if (_camera != null)
            _camera.ResetReplacementShader();
    }
}
