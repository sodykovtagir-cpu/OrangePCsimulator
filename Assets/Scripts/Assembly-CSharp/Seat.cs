using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class Seat : MonoBehaviour, ISave
{

	[SerializeField]
	private Vector3 seatPos;

	[SerializeField]
	private float standDistance = 1.0f;

	[SerializeField]
	private GameObject replacementCollider;

	private Transform t;

	private CharacterController controller;

	private bool isSitting;

	public bool IsSitting => isSitting;

	public void Sit()
	{
		if (isSitting)
			return;

		Player player = Player.Instance;
		if (player == null || player.sit)
			return;

		isSitting = true;

		Raycast ray = FindObjectOfType<Raycast>();
		if (ray != null)
		{
			ray.DragRigidbody = false;
			ray.End();
		}

		Transform playerTransform = player.transform;
		Transform seatTransform = transform;

		if (playerTransform != null)
		{
			t = playerTransform;
			playerTransform.SetParent(seatTransform);

			playerTransform.SetLocalPositionAndRotation(seatPos, Quaternion.identity);
			player.ResetView();

			player.sit = true;

			controller = player.GetComponent<CharacterController>();
			if (controller != null)
			{
				controller.center = new Vector3(0, 1000f, 0);
			}

			if (replacementCollider != null)
			{
				replacementCollider.SetActive(true);
			}
		}
	}

	 private void Update()
    {
        if (!isSitting)
            return;

        float horizontal = InputManager.GetAxis("Horizontal");
        float vertical = InputManager.GetAxis("Vertical");

        if (horizontal != 0f || vertical != 0f)
        {
            isSitting = false;

            Vector3 dir = new Vector3(horizontal, 0f, vertical);

            StartCoroutine(ResetSeat(dir));
        }
    }

	private IEnumerator ResetSeat(Vector3 dir)
    {
        if (t == null || controller == null)
            yield break;

        t.SetParent(null);

        if (replacementCollider != null)
            replacementCollider.SetActive(false);

        float oldHeight = controller.height;
        controller.height = 0f;
        controller.center = Vector3.zero;

        Collider[] cols = GetComponentsInChildren<Collider>();
        foreach (var c in cols)
            c.enabled = false;

        Player player = Player.Instance;
        if (player == null)
            yield break;

        Camera cam = player.GetComponentInChildren<Camera>();
        if (cam == null)
            yield break;

        Transform camTransform = cam.transform;

        Vector3 forward = camTransform.TransformDirection(dir);

        Vector3 up = Vector3.up;
        forward -= Vector3.Dot(forward, up) * up;
        forward.Normalize();

        controller.Move(forward * standDistance);

        yield return new WaitForFixedUpdate();

        controller.height = oldHeight;
        foreach (var c in cols)
            c.enabled = true;

        Vector3 flatForward = camTransform.forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude > 0f)
            t.rotation = Quaternion.LookRotation(flatForward);

        player.sit = false;

        if (Main.Instance != null && Main.Instance.raycast != null)
            Main.Instance.raycast.DragRigidbody = true;
    }

	public void ToData(JObject jObject)
    {
        if (jObject == null)
            throw new System.ArgumentNullException(nameof(jObject));

        JToken value = JToken.FromObject(isSitting);
        jObject["sit"] = value;
    }

	public void FromData(JObject jObject)
    {
        if (jObject == null)
            throw new System.ArgumentNullException(nameof(jObject));

        JToken value = jObject["sit"];
        if (value != null)
        {
            bool sit = value.Value<bool>();
            if (sit)
            {
                Sit();
            }
        }
    }
}
