using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Bomb : MonoBehaviour
{
    [SerializeField] private GameObject explode;
    [SerializeField] private float threshold = 10f;
    private bool played;

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.relativeVelocity.magnitude >= threshold)
            Explode();
    }

    public virtual void Explode()
    {
        if (played || explode == null) return;
        played = true;
        Instantiate(explode, transform.position, transform.rotation);
        Destroy(gameObject);
    }
}
