using UnityEngine;
using UnityEngine.Events;

public class Receiver : MonoBehaviour, IReceiverDown
{
	public UnityEvent OnClick;

	public void Hit()
	{
		OnClick.Invoke();
	}
}
