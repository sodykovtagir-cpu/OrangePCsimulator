using UnityEngine;

public class Connector : MonoBehaviour
{
	[HideInInspector]
	public Slot slot;

	public void Break()
	{
		Destroy(GetComponent<FixedJoint>());
		slot.UnPlug();
		Invoke("Wait", 1f);
	}

	private void OnJointBreak()
	{
		slot.UnPlug();
		Invoke("Wait", 1f);
	}

	private void Wait()
	{
		Destroy(this);
	}
}
