using UnityEngine;

public class ControlRig : MonoBehaviour
{
	public GameObject mobile;

	public GameObject standalone;

	private void Start()
	{
#if UNITY_ANDROID || UNITY_IOS
		standalone.SetActive(false);
		mobile.SetActive(true);
#elif UNITY_STANDALONE
		standalone.SetActive(true);
		mobile.SetActive(false);
#endif
	}
}
