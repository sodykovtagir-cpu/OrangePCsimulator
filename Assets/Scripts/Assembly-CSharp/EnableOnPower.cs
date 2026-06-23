using PC.Component;
using UnityEngine;

public class EnableOnPower : MonoBehaviour
{
	public GameObject obj;

	private void Start()
	{
		var hardware = GetComponentInParent<Hardware>();
		hardware.PowerChanged += UpdatePower;
	}

	public void UpdatePower(bool value)
    {
		obj.SetActive(value);
    }
}
