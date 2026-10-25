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

	[SerializeField]
	private bool removeModeOnly;

	private Rigidbody rb;

	private AudioSource source;

	private float time;

	private float unhookCooldown;

	private bool hooked;

	public bool RemoveModeOnly
	{
		get => removeModeOnly;
		set => removeModeOnly = value;
	}

	public bool Hooked
	{
		get => hooked;
		set => SetHooked(value);
	}

	private void SetHooked(bool value)
	{
		hooked = value;
        if (GetComponent<PC.Component.Cabling.OutletStrip>() != null)
            PC.Component.Cabling.CablePower.RefreshAll();
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
        bool outlet = GetComponent<PC.Component.Cabling.OutletStrip>() != null;

        RaycastHit[] hits = Physics.RaycastAll(origin, dir, detectDistance);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (hit.collider == null || hit.collider.isTrigger)
                continue;
            if (hit.transform == tr || hit.transform.IsChildOf(tr))
                continue;

            // Outlets only attach when the back (-Y) is already nearly facing
            // a vertical wall. Never rotate a hand-placed model to force a fit.
            // Non-outlet Hook items retain their existing surface behaviour.
            if (outlet && (Mathf.Abs(hit.normal.y) > .35f ||
                           Vector3.Dot(dir, -hit.normal) < .9f))
                continue;

            if (hit.transform.CompareTag("Wall") || (hit.collider.attachedRigidbody == null && !hit.transform.CompareTag("Player")))
                return true;
        }
        return false;
    }

	private void OnCollisionEnter(Collision collision)
	{
		if (hooked && !removeModeOnly)
		{
			if (collision.transform.CompareTag("Hammer"))
			{
				Release();
			}
		}
	}

    private void Fix()
    {
        // Freeze the exact pose the player gave the outlet; do not snap or turn it.
        Hooked = true;
        if (fixSound != null && source != null) source.PlayOneShot(fixSound);
    }

	public void Release()
	{
		Hooked = false;
		unhookCooldown = 1.0f;
		time = 0f;
		if (releaseSound != null && source != null) source.PlayOneShot(releaseSound);
	}

	public void ShowTip()
	{
		if (!Hooked) return;
		if (removeModeOnly)
		{
			Main.Instance?.FadeText(Localization.GetText("Click to Remove Component"));
		}
		else
		{
			Main.Instance?.FadeText(Localization.GetText("Remove with hammer"));
		}
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
        bool savedHooked = jObject.Value<bool>("hooked");
        // Recheck old wall outlets without changing their saved pose. If one
        // faces the wrong way it detaches, so the player can place it manually.
        if (savedHooked && GetComponent<PC.Component.Cabling.OutletStrip>() != null)
        {
            Physics.SyncTransforms();
            savedHooked = IsWall();
        }
		Hooked = savedHooked;
		base.FromData(jObject);
	}
}
