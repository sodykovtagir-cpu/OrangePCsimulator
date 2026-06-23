using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MessageBox : MonoBehaviour
{
	[SerializeField]
	private string openParameter;

	[SerializeField]
	private Text text;

	private Animator animator;

	private Queue<string> messages;

	private bool displaying;

	private void Awake()
	{
		animator = GetComponent<Animator>();
		messages = new Queue<string>();
	}

	public void Show(string message)
	{
		messages.Enqueue(message);
		if (!displaying)
		{
			string next = messages.Dequeue();
			text.text = next;
			animator.SetBool(openParameter, true);
			displaying = true;
		}
	}

	public void Ok()
	{
		animator.SetBool(openParameter, false);
		if (messages.Count > 0)
		{
			string next = messages.Dequeue();
			text.text = next;
			animator.SetBool(openParameter, true);
			displaying = true;
		}
		else
		{
			displaying = false;
		}
	}
}
