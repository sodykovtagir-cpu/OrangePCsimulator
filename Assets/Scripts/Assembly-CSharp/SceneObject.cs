using Newtonsoft.Json.Linq;
using UnityEngine;

public abstract class SceneObject : MonoBehaviour, ISave
{
	public string id;

	public abstract void FromData(JObject jObject);

	public abstract void ToData(JObject jObject);
}
