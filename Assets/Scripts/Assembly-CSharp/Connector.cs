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
#if UNITY_EDITOR
		if (CableStressMode.Recording)
		{
			var item = GetComponent<Item>();
			var host = slot != null ? slot.GetComponentInParent<Item>() : null;
			Debug.LogWarning($"Cable stress: SLOT JOINT BREAK item={item?.name} id={item?.Id} " +
				$"host={host?.name} id={host?.Id} slot={slot?.name}");
		}
#endif
		slot.UnPlug();
		Invoke("Wait", 1f);
	}

	private void Wait()
	{
		Destroy(this);
	}
}
