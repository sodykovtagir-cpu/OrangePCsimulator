using UnityEngine;
using UnityEngine.UI;

public class TutorialTip : MonoBehaviour
{
	[SerializeField]
	private Text title;

	[SerializeField]
	private Text continueText;

	private Animator animator;

	private bool played;

	private void Start()
	{
		animator = GetComponent<Animator>();
		var cont = Localization.GetText("click to continue") + " >";
		if (continueText != null) continueText.text = cont;
		if (title != null) title.text = Localization.GetText("How to Remove Component");
	}

	public void JumpTip()
	{
		if (played) return;
		if (animator == null) return;
		animator.SetBool("Open", true);
		played = true;
	}

	public void Continue()
	{
		if (animator != null) animator.SetBool("Open", false);
	}
}
