using UnityEngine;

public class DoAnimation : MonoBehaviour
{
	[SerializeField]
	private Animator animator;

	[SerializeField]
	private string parameter;

	private bool played = false;

	public void Play()
	{
		played = !played;
		animator.SetBool(parameter, played);
	}
}
