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

	private Animator Anim()
	{
		if (animator == null) animator = GetComponent<Animator>();
		return animator;
	}

	public void Show(Action callback)
	{
		this.callback = callback;
		if (!gameObject.activeSelf) gameObject.SetActive(true);
		var a = Anim();
		if (a != null && !string.IsNullOrEmpty(parameter))
			a.SetBool(parameter, true);
	}

	public void Yes()
	{
		callback?.Invoke();
		var a = Anim();
		if (a != null && !string.IsNullOrEmpty(parameter))
			a.SetBool(parameter, false);
	}

	public void No()
	{
		var a = Anim();
		if (a != null && !string.IsNullOrEmpty(parameter))
			a.SetBool(parameter, false);
	}
}
