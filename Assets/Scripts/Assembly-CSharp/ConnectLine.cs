using UnityEngine;

public class ConnectLine : MonoBehaviour
{
	public Transform a;

	public Transform b;

	private LineRenderer line;
	
	private void Start()
	{
		line = GetComponent<LineRenderer>();
	}

	private void Update()
	{
		if (a != null && b != null)
		{
			if (line != null)
			{
				line.SetPosition(0, a.position);
				line.SetPosition(1, b.position);
			}
		}
		else
		{
			Destroy(gameObject);
		}
	}
}
