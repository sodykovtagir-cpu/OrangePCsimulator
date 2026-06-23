using System;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public class Interact : MonoBehaviour
{
	private enum ActiveType
	{
		OnTriggerEnter = 0,
		OnEnable = 1
	}

	[SerializeField]
	private string id;

	[SerializeField]
	private ActiveType activeType = ActiveType.OnEnable;

	[SerializeField]
	private bool callOnce;

	public UnityEvent onInteract;

	public string Id
	{
		get
		{
			return id;
		}
		private set
        {
			id = value;
        }
	}

	private void OnEnable()
	{
		if (activeType == ActiveType.OnEnable) Show();
	}

	private void OnDisable()
	{
		var mgr = InteractManager.Instance;
		if (mgr != null) mgr.RemoveFromList(this);
	}

	private void OnTriggerEnter(Collider other)
	{
		if (other == null) return;
		if (other.CompareTag("Player") && activeType == ActiveType.OnTriggerEnter) Show();
	}

	private void OnTriggerExit(Collider other)
	{
		if (other == null) return;
		if (other.CompareTag("Player") && activeType == ActiveType.OnTriggerEnter) Hide();
	}

	public void Show()
	{
		var mgr = InteractManager.Instance;
		if (mgr != null) mgr.AddToList(this);
	}

	public void Hide()
	{
		var mgr = InteractManager.Instance;
		if (mgr != null) mgr.RemoveFromList(this);
	}

	public void OnInteract()
	{
		if (onInteract == null) return;
		onInteract.Invoke();
		if (callOnce) Destroy(this);
	}
}
