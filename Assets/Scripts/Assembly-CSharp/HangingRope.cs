using UnityEngine;

public class HangingRope : MonoBehaviour
{
	public Transform pointA;

	public Transform pointB;

	public int pointCount = 20;

	public float ropeLength = 10f;

	private LineRenderer line;

	private void Start()
	{
		line = GetComponent<LineRenderer>();
		if (line != null) line.positionCount = pointCount;
	}

	private void Update()
	{
		if (pointA == null || pointB == null || line == null) return;

		var a = pointA.position;
		var b = pointB.position;

		line.SetPosition(0, a);
		line.SetPosition(pointCount - 1, b);

		float distance = Vector3.Distance(a, b);
		float tight = Mathf.Min(ropeLength, distance);
		float slack = ropeLength - tight;

		if (pointCount > 2)
		{
			for (int i = 1; i < pointCount - 1; i++)
			{
				float t = i / (float)(pointCount - 1);
				t = Mathf.Clamp01(t);
				var p = Vector3.Lerp(a, b, t);
				p.y -= (slack * 2f) * t * (1f - t);
				line.SetPosition(i, p);
			}
		}
	}
}
