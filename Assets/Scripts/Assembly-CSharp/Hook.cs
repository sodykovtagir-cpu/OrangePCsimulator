using Newtonsoft.Json.Linq;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(AudioSource))]
public class Hook : Item, ISave
{
	[SerializeField]
	private float detectInterval;

	[SerializeField]
	private float detectDistance;

	[SerializeField]
	private Vector3 hookOffset;

	[SerializeField]
	private Vector3 hookDirection = Vector3.zero;

	[SerializeField]
	private AudioClip fixSound;

	[SerializeField]
	private AudioClip releaseSound;

	private Rigidbody rb;

	private AudioSource source;

	private float time;

	private float unhookCooldown;

	private bool hooked;

	public bool Hooked
	{
		get => hooked;
		set => SetHooked(value);
	}

	private void SetHooked(bool value)
	{
		hooked = value;
		if (rb != null)
		{
			rb.isKinematic = value;
		}
	}

	private void Awake()
	{
		rb = GetComponent<Rigidbody>();
		source = GetComponent<AudioSource>();
	}

	private void Update()
	{
		if (!hooked)
		{
			if (unhookCooldown > 0f)
			{
				unhookCooldown -= Time.deltaTime;
				return;
			}

			time += Time.deltaTime;
			if (time >= detectInterval)
			{
				time = 0f;
				if (IsWall())
				{
					Fix();
				}
			}
		}
	}

	private bool IsWall()
	{
		var tr = transform;
		Vector3 origin = tr.position + tr.TransformDirection(hookOffset);
		Vector3 dir = hookDirection != Vector3.zero
			? tr.TransformDirection(hookDirection.normalized)
			: -tr.forward;

		RaycastHit[] hits = Physics.RaycastAll(origin, dir, detectDistance);
		System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

		for (int i = 0; i < hits.Length; i++)
		{
			var hit = hits[i];
			if (hit.collider == null || hit.collider.isTrigger)
				continue;
			if (hit.transform == tr || hit.transform.IsChildOf(tr))
				continue;

			if (hit.transform.CompareTag("Wall") || (hit.collider.attachedRigidbody == null && !hit.transform.CompareTag("Player")))
				return true;
		}
		return false;
	}

	private void OnCollisionEnter(Collision collision)
	{
		if (hooked)
		{
			if (collision.transform.CompareTag("Hammer"))
			{
				Release();
			}
		}
	}

	private void Fix()
	{
		Hooked = true;
		if (fixSound != null && source != null) source.PlayOneShot(fixSound);
	}

	private void Release()
	{
		Hooked = false;
		unhookCooldown = 1.0f;
		time = 0f;
		if (releaseSound != null && source != null) source.PlayOneShot(releaseSound);
	}

	public void ShowTip()
	{
		if (!Hooked) return;
		Main.Instance?.FadeText(Localization.GetText("Remove with hammer"));
	}

	public void OnDrawGizmos()
	{
		var tr = GetComponent<Transform>();
		if (tr != null)
		{
			Vector3 position = tr.position;
			Vector3 dir = tr.TransformDirection(hookOffset);
			Vector3 forward = hookDirection != Vector3.zero
				? tr.TransformDirection(hookDirection.normalized)
				: -tr.forward;
			Color color = new Color(1f, 0f, 0f, 1f);
			Vector3 start = position + dir;
			Vector3 end = forward * detectDistance;
			Debug.DrawRay(start, end, color);
		}
	}

	public override void ToData(JObject jObject)
	{
		jObject.Add("hooked", Hooked);
		base.ToData(jObject);
	}

	public override void FromData(JObject jObject)
	{
		Hooked = jObject.Value<bool>("hooked");
		base.FromData(jObject);
	}
}
