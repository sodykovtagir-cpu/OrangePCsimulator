using UnityEngine;

public class ScrollLine : MonoBehaviour
{
    [SerializeField]
    private float scrollSpeed = 0.1f;

    private Renderer rend;

    private void Start()
    {
        rend = GetComponent<Renderer>();
    }

    private void Update()
    {
        float offset = Time.time * scrollSpeed;
        if (rend != null && rend.material != null)
        {
            rend.material.mainTextureOffset = new Vector2(offset, 0f);
        }
    }
}
