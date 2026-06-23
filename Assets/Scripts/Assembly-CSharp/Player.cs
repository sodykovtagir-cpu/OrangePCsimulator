using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;

public class Player : MonoBehaviour
{
	[SerializeField]
	private float walkSpeed = 5f;

	[SerializeField]
	private float runSpeed = 10f;

	[SerializeField]
	private float sensitivity = 1f;

	[SerializeField]
	private float footstepInterval = 0.5f;

	[SerializeField]
	private float runstepLenghten = 1.3f;

	[SerializeField]
	private float viewAngle = 80f;

	[SerializeField]
	private AudioClip[] concreteFootsteps;

	[SerializeField]
	private AudioClip[] snowFootsteps;

	[SerializeField]
	private AudioSource source;

	[SerializeField]
	private Transform cam;

	[SerializeField]
	private float gravity = -9.8f;

	[SerializeField]
	private float pushForce = 10f;

	public bool sit;

	public bool pauseControls;

	private CharacterController controller;

	private Vector3 playerDirection;

	private Vector3 cameraOld;

	private float clampY;

	private bool isSnow;

	private float footstepTime;

	public float Sensitivity
	{
		get
		{
			return sensitivity;
		}
		set
		{
			sensitivity = value;
		}
	}

	public static Player Instance { get; private set; }

	private void Awake()
	{
		Instance = this;
		controller = GetComponent<CharacterController>();
		if (cam != null)
		{
			cameraOld = cam.localPosition;
			return;
		}
	}

private void Update()
{
    if (pauseControls) return;

    Cursor.lockState = CursorLockMode.Locked;
    Cursor.visible = false;

    float mouseX = InputManager.GetAxis("Mouse X");
    float mouseY = InputManager.GetAxis("Mouse Y");

    float newClampY = clampY - mouseY * sensitivity;
    if (newClampY > viewAngle) newClampY = viewAngle;
    if (newClampY < -viewAngle) newClampY = -viewAngle;
    clampY = newClampY;

    transform.Rotate(0f, mouseX * sensitivity, 0f);

    if (cam != null)
    {
        cam.localRotation = Quaternion.Euler(clampY, 0f, 0f);
    }

    transform.Rotate(
        (Input.GetKey(KeyCode.UpArrow) ? -1f :
         Input.GetKey(KeyCode.DownArrow) ? 1f : 0f) * 15f * Time.deltaTime,

        (Input.GetKey(KeyCode.RightArrow) ? 1f :
         Input.GetKey(KeyCode.LeftArrow) ? -1f : 0f) * 15f * Time.deltaTime,

        0f,
        Space.Self);
}

	private void FixedUpdate()
	{
		bool run = InputManager.GetButton("Run");
		if (sit) return;
		if (pauseControls) return;

		float speed = run ? runSpeed : walkSpeed;
		float h = InputManager.GetAxis("Horizontal");
		float v = InputManager.GetAxis("Vertical");

		playerDirection.x = h * speed;
		playerDirection.z = v * speed;

		Vector3 worldDir = transform.TransformDirection(playerDirection);
		playerDirection = worldDir;

		Vector3 motion = worldDir * Time.fixedDeltaTime;
		controller.Move(motion);

		if (!controller.isGrounded)
		{
			playerDirection.y += gravity * Time.fixedDeltaTime;
			return;
		}

		Vector3 vel = controller.velocity;
		if (vel.sqrMagnitude > 1f && (playerDirection.x != 0f || playerDirection.z != 0f))
		{
			footstepTime += Time.fixedDeltaTime * (run ? runstepLenghten : 1f);
			if (footstepTime >= footstepInterval)
			{
				footstepTime = 0f;
				AudioClip[] clips = isSnow ? snowFootsteps : concreteFootsteps;
				if (clips != null && clips.Length > 0)
				{
					int idx = UnityEngine.Random.Range(0, clips.Length);
					AudioClip clip = clips[idx];
					if (clip != null) source?.PlayOneShot(clip);
				}
			}
		}

		playerDirection.y = gravity;
	}

	private IEnumerator ShakeCamera()
	{
		float time = 1f;
		while (time > 0f)
		{
			time -= Time.deltaTime;
			cam.localPosition = cameraOld + UnityEngine.Random.insideUnitSphere / 2f * time;
			yield return null;
		}
		cam.localPosition = cameraOld;
	}

	public void OnSnow(bool value)
    {
		isSnow = value;
    }

	private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Collider col = hit.collider;
        Rigidbody body = col.attachedRigidbody;
        if (body == null)
            return;

        if (body.isKinematic)
            return;

        Vector3 moveDir = hit.moveDirection;
        if (moveDir.y < -0.3f)
            return;

        Vector3 pushDir = new Vector3(moveDir.x, 0f, moveDir.z);
        body.AddForce(pushDir * pushForce, ForceMode.Force);
    }

	public void ResetView()
	{
		clampY = 0f;
		cam.localRotation = Quaternion.Euler(Vector3.zero);
    }

	public PlayerData SavePlayer()
    {
        PlayerData data = new PlayerData();

        Transform t = transform;

        Vector3 pos = t.position;
        data.x = pos.x;
        data.y = pos.y;
        data.z = pos.z;

        Vector3 euler = t.eulerAngles;
        data.ry = euler.y;

        Vector3 camEuler = cam.localEulerAngles;
        float pitch = camEuler.x % 360f;
        if (pitch < 0f)
            pitch = 0f;
        float rx = (pitch > 180f) ? pitch - 360f : pitch;
        data.rx = rx;

        return data;
    }

	public void LoadPlayer(PlayerData playerData)
    {
        controller.enabled = false;
        float y = playerData.y;
        if (y > -100f && y < 100f)
        {
            transform.position = new Vector3(playerData.x, playerData.y, playerData.z);
        }
        transform.eulerAngles = new Vector3(0f, playerData.ry, 0f);
        clampY = playerData.rx;
        cam.localEulerAngles = new Vector3(clampY, 0f, 0f);
        StartCoroutine(EnableController());
    }

    private IEnumerator EnableController()
    {
        yield return null;
        controller.enabled = true;
    }
}
