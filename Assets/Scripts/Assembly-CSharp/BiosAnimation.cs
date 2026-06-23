using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

public class BiosAnimation : MonoBehaviour
{
	[SerializeField]
	private string[] lines;

	[SerializeField]
	private AudioClip typingSound;

	[SerializeField]
	private float showSpeed;

	[SerializeField]
	private float nextLineInterval;

	private Text text;

	private AudioSource source;

	private IEnumerator Start()
	{
		text = GetComponent<Text>();
		source = GetComponent<AudioSource>();
		var showWait = new WaitForSeconds(showSpeed);
		var nextLineWait = new WaitForSeconds(nextLineInterval);
		foreach (var line in lines)
		{
			for (int i = 0; i < line.Length; i++)
			{
				text.text += line[i];
				source?.PlayOneShot(typingSound);
				yield return showWait;
			}
			text.text += "\n";
			yield return nextLineWait;
		}
	}
}
