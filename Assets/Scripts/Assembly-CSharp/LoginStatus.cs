using UnityEngine;
using UnityEngine.UI;

public class LoginStatus : MonoBehaviour
{
	[SerializeField]
	private Image loginIcon;

	private void Start()
	{
//		CloudOnce.Cloud.OnSignedInChanged += RefreshLogin;
		RefreshLogin(/*CloudOnce.Cloud.IsSignedIn*/ true);
	}

	private void OnDestroy()
	{
//		CloudOnce.Cloud.OnSignedInChanged -= RefreshLogin;
	}

	private void RefreshLogin(bool isSignedIn)
	{
		if (loginIcon == null) return;
		var on = new UnityEngine.Color(0.196f, 1f, 0.196f, 1f);
		var off = new UnityEngine.Color(1f, 0.196f, 0.196f, 1f);
//		loginIcon.color = isSignedIn ? on : off;
	}
}
