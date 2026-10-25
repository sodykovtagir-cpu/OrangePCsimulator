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

    // For wall outlets only: distance from the root to the back of the plate,
    // along local -Y. Keep it editable when the artist changes the model.
    [SerializeField]
    private float outletBackDepth = .31f;

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
                RaycastHit surface;
                if (TryGetSurface(out surface))
                {
                    Fix(surface);
                }
			}
		}
	}

    private bool TryGetSurface(out RaycastHit surface)
    {
        surface = default(RaycastHit);
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

            // Outlets may mount only with their back (-Y) facing a near-vertical
            // wall, not by grazing a wall edge or touching the floor. Keep the
            // old any-static-surface behaviour for every other Hook item.
            if (outlet && (Mathf.Abs(hit.normal.y) > .35f ||
                           Vector3.Dot(dir, -hit.normal) < .35f))
                continue;

            if (hit.transform.CompareTag("Wall") || (hit.collider.attachedRigidbody == null && !hit.transform.CompareTag("Player")))
            {
                surface = hit;
                return true;
            }
        }
        return false;
    }

    private void SnapOutletToWall(RaycastHit hit)
    {
        // Local +Y is the visible socket face; local +Z follows the row of
        // sockets. Align the face outward and keep the row upright.
        Vector3 face = hit.normal;
        Vector3 row = Vector3.ProjectOnPlane(Vector3.up, face).normalized;
        Quaternion rotation = Quaternion.LookRotation(row, face);
        // The solid body extends behind the port pivots; place its rear surface
        // against the wall rather than burying half the outlet in the wall.
        float depth = Mathf.Max(0f, outletBackDepth) * Mathf.Abs(transform.lossyScale.y);
        Vector3 position = hit.point + face * (depth + .003f);
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        transform.SetPositionAndRotation(position, rotation);
        Physics.SyncTransforms();
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

    private void Fix(RaycastHit surface)
    {
        if (GetComponent<PC.Component.Cabling.OutletStrip>() != null)
            SnapOutletToWall(surface);
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
        // Old saves may have an outlet fixed sideways or to the floor. Only
        // outlets are rechecked; other Hook items keep their saved state.
        if (savedHooked && GetComponent<PC.Component.Cabling.OutletStrip>() != null)
        {
            Physics.SyncTransforms();
            RaycastHit surface;
            savedHooked = TryGetSurface(out surface);
            if (savedHooked)
                SnapOutletToWall(surface);
        }
		Hooked = savedHooked;
		base.FromData(jObject);
	}
}
