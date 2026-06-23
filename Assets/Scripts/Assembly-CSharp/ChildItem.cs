using Newtonsoft.Json.Linq;
using UnityEngine;

public class ChildItem : MonoBehaviour, ISave
{
	private class Data
	{
		public Vector3 pos;

		public Quaternion rot;
	}

	[SerializeField]
	private Transform target;

	[SerializeField]
	private string id = "child";

	public void FromData(JObject jObject)
	{
		if (jObject == null) return;

		var token = jObject[id] as JObject;
		if (token == null) return;

		var posToken = token["pos"] as JObject;
		var rotToken = token["rot"] as JObject;
		if (posToken == null || rotToken == null) return;

		Vector3 pos = new Vector3(
			posToken["x"].ToObject<float>(),
			posToken["y"].ToObject<float>(),
			posToken["z"].ToObject<float>()
		);

		Quaternion rot = new Quaternion(
			rotToken["x"].ToObject<float>(),
			rotToken["y"].ToObject<float>(),
			rotToken["z"].ToObject<float>(),
			rotToken["w"].ToObject<float>()
		);

		if (target != null)
		{
			target.position = pos;
			target.rotation = rot;
		}
	}

	public void ToData(JObject jObject)
	{
		if (jObject == null) return;
		if (target == null) return;

		var pos = target.position;
		var rot = target.rotation;

		var obj = new JObject
		{
			["pos"] = new JObject
			{
				["x"] = pos.x,
				["y"] = pos.y,
				["z"] = pos.z
			},
			["rot"] = new JObject
			{
				["x"] = rot.x,
				["y"] = rot.y,
				["z"] = rot.z,
				["w"] = rot.w
			}
		};

		jObject[id] = obj;
	}
}
