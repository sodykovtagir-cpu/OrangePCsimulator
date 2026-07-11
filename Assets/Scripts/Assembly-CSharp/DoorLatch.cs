using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(HingeJoint))]
public class DoorLatch : MonoBehaviour
{
    [SerializeField]
    private AudioClip latchSound;

    [SerializeField]
    private AudioClip unlatchSound;

    private Rigidbody rb;
    private HingeJoint hinge;
    private AudioSource source;
    private bool isLatched = true;

    public bool IsLatched => isLatched;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        hinge = GetComponent<HingeJoint>();
        source = GetComponent<AudioSource>();

        Latch();
    }

    public void Latch()
    {
        isLatched = true;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Заморозить все вращения
        rb.constraints = RigidbodyConstraints.FreezeAll;

        if (latchSound != null && source != null)
            source.PlayOneShot(latchSound);
    }

    public void Unlatch()
    {
        isLatched = false;

        // Разморозить
        rb.constraints = RigidbodyConstraints.None;

        if (unlatchSound != null && source != null)
            source.PlayOneShot(unlatchSound);
    }
}