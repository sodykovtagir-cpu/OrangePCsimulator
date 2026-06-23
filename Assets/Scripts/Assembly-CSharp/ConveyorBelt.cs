using UnityEngine;

public class ConveyorBelt : MonoBehaviour
{
	public Vector3 moveDir;

	private Rigidbody rb;

	private void Start()
    {
		rb = GetComponent<Rigidbody>();
    }

	private void FixedUpdate()
    {
        if (rb == null) return;

        Vector3 position = rb.position;
        Vector3 current = rb.position;

        float dt = Time.fixedDeltaTime;

        Vector3 value = new Vector3(
            current.x + moveDir.x * dt,
            current.y + moveDir.y * dt,
            current.z + moveDir.z * dt
        );

        rb.position = value;

        if (rb != null)
        {
            rb.MovePosition(position);
        }
    }
}
