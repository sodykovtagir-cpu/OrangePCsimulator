using UnityEngine;

public class FanSpin : MonoBehaviour
{
	[SerializeField]
	private float speed = 400f;

	[SerializeField]
	private float maxSpeed = 400f;

	[SerializeField]
	private float damping = 50f;

	[SerializeField]
	private Material normalMaterial;

	[SerializeField]
	private Material blurMaterial;

	private Renderer fan;

	private bool isBlur;

	private bool running;

	public float Velocity { get; private set; }

	public float MaxSpeed => maxSpeed;

	private void Start()
    {
        var hardware = GetComponentInParent<PC.Component.Hardware>();
        if (hardware == null) return;
        hardware.PowerChanged += UpdatePower;
        fan = GetComponent<Renderer>();
    }

	private void Update()
	{
		float v = Velocity;

		if (!running)
		{
			if (v > 0f)
			{
				v -= Time.deltaTime * damping;
				if (v < 0f) v = 0f;
				Velocity = v;
			}
			else
			{
				Velocity = 0f;
				if (isBlur)
				{
					if (fan != null) fan.material = normalMaterial;
					isBlur = false;
				}

				return;
			}
		}
		else
		{
			if (v < maxSpeed)
			{
				v += Time.deltaTime * speed;
				Velocity = v;
			}

			if (v <= 300f)
			{
				if (isBlur)
				{
					if (fan != null) fan.material = normalMaterial;
					isBlur = false;
				}
			}
			else if (!isBlur)
			{
				if (fan != null) fan.material = blurMaterial;
				isBlur = true;
			}
		}

		if (Velocity <= 0f) return;

		float delta = Velocity * Time.deltaTime;
		transform.Rotate(0f, delta, 0f, Space.Self);
	}

	public void UpdatePower(bool value)
    {
		running = value;
    }
}
