using UnityEngine;

public class SwitchGameObjectByPlatform : MonoBehaviour
{
	[SerializeField]
	private GameObject android;

	[SerializeField]
	private GameObject ios;

	private void Awake()
	{
#if UNITY_ANDROID
		android.SetActive(true);
		ios.SetActive(false);
#elif UNITY_IOS
		android.SetActive(false);
		ios.SetActive(true);
#endif
	}
}
