using UnityEngine;

public class ScrollingUV_Layers : MonoBehaviour
{
    public Vector2 uvAnimationRate;
    public string textureName = "_MainTex";
    private Vector2 uvOffset;

    private void LateUpdate()
    {
        uvOffset += uvAnimationRate * Time.deltaTime;

        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null && renderer.sharedMaterial != null)
        {
            renderer.sharedMaterial.SetTextureOffset(textureName, uvOffset);
        }
    }
}
