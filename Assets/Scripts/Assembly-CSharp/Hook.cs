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
	private AudioClip fixSound;

	[SerializeField]
	private AudioClip releaseSound;

	private Rigidbody rb;

	private AudioSource source;

	private float time;

	private bool lastWall;

	private bool hooked;

	private bool Hooked
	{
		get => hooked;
		set
		{
			hooked = value;
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
			time += Time.deltaTime;
			if (time >= detectInterval)
			{
				time = 0f;
				bool currentWall = IsWall();
				if (lastWall != currentWall)
				{
					if (currentWall) Fix();
					lastWall = currentWall;
				}
			}
		}
	}

	private bool IsWall()
	{
		var tr = transform;
		Vector3 origin = tr.position + tr.TransformDirection(hookOffset);
		Vector3 dir = -tr.forward;
		if (Physics.Raycast(origin, dir, out RaycastHit hit, detectDistance))
		{
			if (hit.transform != null && hit.transform.CompareTag("Wall"))
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
		if (releaseSound != null && source != null) source.PlayOneShot(releaseSound);
	}

	public void ShowTip()
	{
		if (!Hooked) return;
		Main.Instance.FadeText(Localization.GetText("Remove with hammer"));
	}

	public void OnDrawGizmos()
    {
        var tr = GetComponent<Transform>();
        if (tr != null)
        {
            Vector3 position = tr.position;
            Vector3 dir = tr.TransformDirection(hookOffset);
            Vector3 forward = tr.forward;
            Color color = new Color(1f, 0f, 0f, 1f);
            Vector3 start = position + dir;
            Vector3 end = -forward * detectDistance;
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
