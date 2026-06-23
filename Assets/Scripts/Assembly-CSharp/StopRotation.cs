using UnityEngine;

public class StopRotation : MonoBehaviour
{
	private Quaternion old;

	private void Start()
	{
		old = transform.rotation;
	}

	private void Update()
	{
		transform.rotation = old;
	}
}
