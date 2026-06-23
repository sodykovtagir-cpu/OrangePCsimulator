using System;
using UnityEngine;

public class ConfirmationDialog : MonoBehaviour
{
	[SerializeField]
	private string parameter;

	private Animator animator;

	private Action callback;

	private void Start()
	{
		animator = GetComponent<Animator>();
	}

	public void Show(Action callback)
	{
		this.callback = callback;
		animator.SetBool(parameter, true);
	}

	public void Yes()
	{
		callback.Invoke();
		animator.SetBool(parameter, false);
	}

	public void No()
	{
		animator.SetBool(parameter, false);
	}
}
