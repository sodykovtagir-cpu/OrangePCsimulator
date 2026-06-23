using UnityEngine;

namespace PC.VR
{
	public class Player : MonoBehaviour
	{
		[SerializeField]
		private float speed = 5;

		[SerializeField]
		private float sensitivity = 1;

		[SerializeField]
		private float footstepInterval = 0.5f;

		[SerializeField]
		private float viewAngle;

		[SerializeField]
		private AudioClip[] footsteps;

		[SerializeField]
		private AudioSource source;

		[SerializeField]
		private Transform cam;

		[SerializeField]
		private float gravity = -9.8f;

		[SerializeField]
		private float pushForce = 10;

		[SerializeField]
		private float maxDistance = 20;

		[SerializeField]
		private LayerMask raycastLayer;

		public bool pauseControls;

		private CharacterController controller;

		private Vector3 playerDirection;

		private float clampY;

		private float footstepTime = 0.5f;

		private Camera mainCamera;

		public float Sensitivity { get { return sensitivity; } set { sensitivity = value; }  }

		private void Start()
		{
			controller = GetComponent<CharacterController>();
			if (cam != null) mainCamera = cam.GetComponent<Camera>();
		}

		private void Update()
		{
			if (!pauseControls)
			{
				var mx = InputManager.GetAxis("Mouse X");
				var my = InputManager.GetAxis("Mouse Y");
				clampY = Mathf.Clamp(clampY - my * sensitivity, -viewAngle, viewAngle);
				transform.Rotate(0f, mx * sensitivity, 0f);
				if (cam != null) cam.localRotation = Quaternion.Euler(clampY, 0f, 0f);
				playerDirection.x = InputManager.GetAxis("Horizontal") * speed;
				playerDirection.z = InputManager.GetAxis("Vertical") * speed;
			}
			else
			{
				playerDirection.x = 0f;
				playerDirection.z = 0f;
			}

			var worldDir = transform.TransformDirection(playerDirection);
			var dt = Time.deltaTime;
			if (controller != null) controller.Move(worldDir * dt);

			if (controller != null && controller.isGrounded)
			{
				var v = controller.velocity;
				if (v.sqrMagnitude > 1f)
				{
					if (footstepTime >= footstepInterval)
					{
						footstepTime = 0f;
						if (footsteps != null && footsteps.Length > 0 && source != null)
						{
							int i = footsteps.Length > 1 ? Random.Range(1, footsteps.Length) : 0;
							var clip = footsteps[i];
							source.PlayOneShot(clip);
							if (footsteps.Length > 1)
							{
								var first = footsteps[0];
								footsteps[0] = clip;
								footsteps[i] = first;
							}
						}
					}
					else
					{
						footstepTime += dt;
					}
				}
				playerDirection.y = gravity;
			}
			else
			{
				playerDirection.y += gravity * dt;
			}
		}

		public void Touch()
		{
			int count = Input.touchCount;
			if (count <= 0) return;
			var touches = Input.touches;
			for (int i = 0; i < touches.Length; i++)
			{
				var t = touches[i];
				if (t.phase == TouchPhase.Began)
				{
					var pos = new Vector3(t.position.x, t.position.y, 0f);
					ShootRaycast(pos);
				}
			}
		}

		private void ShootRaycast(Vector3 pos)
		{
			if (mainCamera == null) return;
			var ray = mainCamera.ScreenPointToRay(pos);
			if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, raycastLayer))
			{
				var col = hit.collider;
				if (col == null) return;
				if (col.TryGetComponent<IReceiverDown>(out var r)) r.Hit();
			}
		}

		private void OnControllerColliderHit(ControllerColliderHit hit)
		{
			if (hit == null) return;
			var col = hit.collider;
			if (col == null) return;
			var rb = col.attachedRigidbody;
			if (rb == null || rb.isKinematic) return;
			var dir = hit.moveDirection;
			if (dir.y < -0.3f) return;
			var force = new Vector3(dir.x * pushForce, 0f, dir.z * pushForce);
			rb.AddForce(force);
		}
	}
}
