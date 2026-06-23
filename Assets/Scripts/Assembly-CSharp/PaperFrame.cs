using UnityEngine;

public class PaperFrame : MonoBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null) return;

        var obj = collision.gameObject;
        if (obj == null || !obj.CompareTag("Paper")) return;

        var src = obj.GetComponent<TextureLoader>();
        if (src == null || src.IsEmpty()) return;

        var dst = GetComponent<TextureLoader>();
        if (dst == null || !dst.IsEmpty()) return;

        dst.CopyFromLoader(src);

        Destroy(obj);
    }
}
