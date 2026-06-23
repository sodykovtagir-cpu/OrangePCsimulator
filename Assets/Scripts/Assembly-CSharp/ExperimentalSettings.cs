using UnityEngine;
using UnityEngine.UI;
using System;
public class ExperimentalSettings : MonoBehaviour
{
	[SerializeField]
	private GameObject gallery;

	[SerializeField]
	private Text randomTest;

	private void Start()
	{
		if (PlayerPrefs.GetInt("Experimental", 0) == 1)
		{
			gallery.SetActive(true);
			System.Random r = new System.Random(DateTime.UtcNow.DayOfYear);
			randomTest.text = string.Concat(r.Next(),",",r.Next(),",",r.Next());
		}
	}

	public void EnableExperimental()
	{
		PlayerPrefs.SetInt("Experimental", 1);
		this.gallery.SetActive(true);
	}
}
