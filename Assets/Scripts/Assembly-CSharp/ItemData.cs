using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

[Serializable]
public class ItemData
{
	public string spawnId;

	public int id;

	public Vector3 pos;

	public Quaternion rot;

	public JObject data;
}
