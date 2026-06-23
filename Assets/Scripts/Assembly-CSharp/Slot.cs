using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

public class Slot : MonoBehaviour
{
	public string target;

	[SerializeField]
	private byte[] matches;

	[SerializeField]
	private Transform insertPos;

	[SerializeField]
	private float breakForce = 200f;

	[SerializeField]
	private bool setParent = true;

	[SerializeField]
	private AudioClip plug;

	[SerializeField]
	private AudioClip unplug;

	private bool preparing = true;

	private bool isUsing;

	private Transform attachedTransform;

	private Item attachedItem;

	private MeshRenderer hintRenderer;

	private IEnumerator Start()
	{
		preparing = true;
		yield return new WaitForSeconds(1f);
		preparing = false;
	}

	public void EnableHint()
	{
		GameObject hintCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
		Destroy(hintCube.GetComponent<BoxCollider>());

		hintRenderer = hintCube.GetComponent<MeshRenderer>();
		hintRenderer.material = Resources.Load<Material>("Hint");

		hintCube.transform.SetParent(transform);
		
		BoxCollider col = GetComponent<BoxCollider>();
		hintCube.transform.localPosition = col.center;
		hintCube.transform.localRotation = Quaternion.identity;
		hintCube.transform.localScale = col.size;
		
		hintRenderer.enabled = false;
	}

	private void OnTriggerEnter(Collider col)
	{
		if (!isUsing)
		{
			if (col.CompareTag(target))
			{
				if (col.GetComponent<Connector>() == null)
				{
					Item it;
					bool s = col.TryGetComponent<Item>(out it);
					if (s)
					{
						bool match = IsMatch(it.Match);
						if (match)
						{
							isUsing = true;
							SetComponent(it);
						}
					}
				}
			}
		}
	}

	public void UnPlug()
	{
		if (!isUsing) return;
		isUsing = false;
		RemoveComponent();
	}

	protected virtual void SetComponent(Item item)
	{
		Transform itrans = item.transform;

		item.transform.rotation = insertPos.rotation;
		Vector3 worldOffset = item.transform.TransformVector(item.SlotOffset);
		item.transform.position = insertPos.position + worldOffset;

		if (setParent)
			itrans.SetParent(insertPos, true);

		FixedJoint addj = item.gameObject.AddComponent<FixedJoint>();
		if (!item.glue)
			addj.breakForce = breakForce;
		addj.connectedBody = gameObject.GetComponentInParent<Rigidbody>();

		attachedTransform = itrans;
		attachedItem = item;

		Connector conn = itrans.gameObject.AddComponent<Connector>();
		if (conn != null) conn.slot = this;

		item.gameObject.layer = 8;

		if (!preparing && plug)
			AudioSource.PlayClipAtPoint(plug, transform.position);

		item.OnSlotConnected();
	}

	protected virtual void RemoveComponent()
	{
		if (attachedTransform == null) return;
		FixedJoint joint = attachedTransform.GetComponent<FixedJoint>();
		if (joint != null) Destroy(joint);
		attachedItem.glue = false;
		attachedItem.gameObject.layer = 0;
		if (setParent)
			attachedTransform.SetParent(null);
		if (unplug)
			AudioSource.PlayClipAtPoint(unplug, attachedTransform.position);
		attachedItem.OnSlotDisconnected();
		attachedTransform = null;
		attachedItem = null;
		isUsing = false;
	}

	public bool IsMatch(byte b)
	{
		bool match = false;
		if (matches.Length == 0)
		{
			match = true;
		}
		else
		{
			for (int i = 0; i < matches.Length; i++)
			{
				if (matches[i] == b) { match = true;  break; }
			}
		}
		return match;
	}

	public void ShowHint(bool show)
	{
		if (hintRenderer == null) return;
		hintRenderer.enabled = show;
	}
}
