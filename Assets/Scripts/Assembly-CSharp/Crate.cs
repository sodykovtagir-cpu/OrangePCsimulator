using UnityEngine;

public class Crate : MonoBehaviour
{
	[SerializeField]
	private GameObject broken;

	private void OnCollisionEnter(Collision collision)
	{
		if (collision == null) return;
		if (collision.transform == null) return;
		if (!collision.transform.CompareTag("Hammer")) return;
		if (broken == null) return;
		broken.SetActive(true);
		broken.transform.SetParent(null);
		Destroy(gameObject);
	}

	public void ShowTip()
	{
		Main.Instance.FadeText(Localization.GetText("Use Hammer to Open"));
	}
}
