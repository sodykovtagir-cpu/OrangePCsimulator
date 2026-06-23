using UnityEngine;

[ExecuteInEditMode]
public class Liquid : MonoBehaviour
{
	public enum UpdateMode
	{
		Normal = 0,
		UnscaledTime = 1
	}

	public UpdateMode updateMode;

	[SerializeField]
	private float MaxWobble = 0.03f;

	[SerializeField]
	private float WobbleSpeedMove = 1;

	[SerializeField]
	private float fillAmount = 0.5f;

	[SerializeField]
	private float Recovery = 1;

	[SerializeField]
	private float Thickness = 1;

	[Range(0f, 1f)]
	public float CompensateShapeAmount;

	[SerializeField]
	private Mesh mesh;

	[SerializeField]
	private Renderer rend;

	private Vector3 pos;

	private Vector3 lastPos;

	private Vector3 velocity;

	private Quaternion lastRot;

	private Vector3 angularVelocity;

	private float wobbleAmountX;

	private float wobbleAmountZ;

	private float wobbleAmountToAddX;

	private float wobbleAmountToAddZ;

	private float pulse;

	private float sinewave;

	private float time = 0.5f;

	private Vector3 comp;

	private void Start()
	{
		if (mesh == null)
		{
			var mf = GetComponent<UnityEngine.MeshFilter>();
			if (mf != null) mesh = mf.sharedMesh;
		}
		if (rend == null) rend = GetComponent<UnityEngine.Renderer>();
	}

	private void OnValidate()
	{
		if (mesh == null)
		{
			var mf = GetComponent<UnityEngine.MeshFilter>();
			if (mf != null) mesh = mf.sharedMesh;
		}
		if (rend == null) rend = GetComponent<UnityEngine.Renderer>();
	}

	private void GetMeshAndRend()
	{
		if (mesh == null)
		{
			var mf = GetComponent<MeshFilter>();
			if (mf != null) mesh = mf.sharedMesh;
		}
		if (rend == null) rend = GetComponent<Renderer>();
	}

	private void Update()
	{
		float deltaTime = updateMode == UpdateMode.Normal ? UnityEngine.Time.deltaTime : updateMode == UpdateMode.UnscaledTime ? UnityEngine.Time.unscaledDeltaTime : 0f;
		time += deltaTime;

		if (deltaTime != 0f)
		{
			float pulse = WobbleSpeedMove * 6.2831855f;
			float target = UnityEngine.Mathf.Sin(time * pulse);
			float recover = UnityEngine.Mathf.Clamp01(deltaTime * Recovery);
			wobbleAmountToAddX = UnityEngine.Mathf.Lerp(wobbleAmountToAddX, 0f, recover);
			wobbleAmountToAddZ = UnityEngine.Mathf.Lerp(wobbleAmountToAddZ, 0f, recover);

			var pos = transform.position;
			velocity = (lastPos - pos) / deltaTime;

			var rot = transform.rotation;
			angularVelocity = GetAngularVelocity(lastRot, rot);

			float speed = velocity.magnitude + angularVelocity.magnitude;
			speed = UnityEngine.Mathf.Max(speed, Thickness);
			float s = UnityEngine.Mathf.Clamp01(deltaTime * speed);

			sinewave = UnityEngine.Mathf.Lerp(sinewave, target, s);
			wobbleAmountX = wobbleAmountToAddX * sinewave;
			wobbleAmountZ = wobbleAmountToAddZ * sinewave;

			float vy = velocity.y * 0.2f;
			float addX = UnityEngine.Mathf.Clamp(MaxWobble * (angularVelocity.y + angularVelocity.z + velocity.x + vy), -MaxWobble, MaxWobble);
			float addZ = UnityEngine.Mathf.Clamp(MaxWobble * (angularVelocity.y + vy + velocity.z + angularVelocity.x), -MaxWobble, MaxWobble);
			wobbleAmountToAddX += addX;
			wobbleAmountToAddZ += addZ;
		}

		var mat = rend != null ? rend.sharedMaterial : null;
		if (mat != null)
		{
			mat.SetFloat("_WobbleX", wobbleAmountX);
			mat.SetFloat("_WobbleZ", wobbleAmountZ);
		}

		UpdatePos(deltaTime);
		var p = transform.position;
		lastPos = p;
		lastRot = transform.rotation;
	}

	private void UpdatePos(float deltaTime)
	{
		var t = transform;
		if (mesh == null || t == null) return;

		var center = mesh.bounds.center;
		var worldCenter = t.TransformPoint(center);

		float px, py, pz;
		if (CompensateShapeAmount <= 0f)
		{
			var p = t.position;
			px = worldCenter.x - p.x;
			py = worldCenter.y - p.y - fillAmount;
			pz = worldCenter.z - p.z;
		}
		else
		{
			if (deltaTime == 0f)
			{
				comp.x = worldCenter.x;
				comp.y = worldCenter.y - GetLowestPoint();
				comp.z = worldCenter.z;
			}
			else
			{
				float l = deltaTime * 10f;
				if (l < 0f) l = 0f;
				float lowest = GetLowestPoint();
				comp.x = comp.x + (worldCenter.x - comp.x) * l;
				comp.y = comp.y + ((worldCenter.y - lowest) - comp.y) * l;
				comp.z = comp.z + (worldCenter.z - comp.z) * l;
			}

			var p = t.position;
			px = worldCenter.x - p.x;
			py = worldCenter.y - p.y - (fillAmount - comp.y * CompensateShapeAmount);
			pz = worldCenter.z - p.z;
		}

		pos = new UnityEngine.Vector3(px, py, pz);
		var m = rend != null ? rend.sharedMaterial : null;
		if (m != null) m.SetVector("_FillAmount", new UnityEngine.Vector4(pos.x, pos.y, pos.z, 0f));
	}

	private Vector3 GetAngularVelocity(Quaternion foreLastFrameRotation, Quaternion lastFrameRotation)
	{
		var delta = lastFrameRotation * Quaternion.Inverse(foreLastFrameRotation);
		var w = delta.w;
		if (Mathf.Abs(w) > 0.9995117f) return Vector3.zero;
		float s = Mathf.Sqrt(1f - w * w);
		if (s <= 1e-8f || Time.deltaTime <= 0f) return Vector3.zero;
		float angle = w >= 0f ? 2f * Mathf.Acos(w) : -2f * Mathf.Acos(-w);
		var axis = new Vector3(delta.x / s, delta.y / s, delta.z / s);
		return axis * (angle / Time.deltaTime);
	}

	private float GetLowestPoint()
	{
		if (mesh == null) return 0f;
		var verts = mesh.vertices;
		if (verts == null || verts.Length == 0) return 0f;
		float minY = float.MaxValue;
		var t = transform;
		for (int i = 0; i < verts.Length; i++)
		{
			var w = t.TransformPoint(verts[i]);
			if (w.y < minY) minY = w.y;
		}
		return minY == float.MaxValue ? 0f : minY;
	}
}
