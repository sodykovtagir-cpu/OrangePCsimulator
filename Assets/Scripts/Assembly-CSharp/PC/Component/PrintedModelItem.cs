using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using PC.Component;
using UnityEngine;

/// <summary>
/// A finished 3D print. The room save system only knows how to respawn Items
/// from prefabs, so every finished print becomes this Item. The actual runtime
/// mesh is serialized into the item's save data and rebuilt on load; the held
/// object's display name is the source file name without its extension.
/// </summary>
public class PrintedModelItem : Item, ISave
{
	public const string PrintedSpawnId = "PrintedModel";

	[SerializeField] private string modelName = "Printed Model";
	[SerializeField] private string colorHex = "FFFFFF";
	[SerializeField] private float savedMass = 0.1f;
	[SerializeField] private PC.Component.FilamentFinish finish = PC.Component.FilamentFinish.Glossy;
	[SerializeField] private float smoothness = 0.85f;
	[SerializeField] private string glowHex = "000000";
	[SerializeField] private float glowIntensity;

	public string ModelName => modelName;

	private Color GlowColor
	{
		get
		{
			var color = Color.black;
			if (!string.IsNullOrEmpty(glowHex)) ColorUtility.TryParseHtmlString("#" + glowHex, out color);
			return color;
		}
	}

	private Color EmissionColor
	{
		get
		{
			if (finish != PC.Component.FilamentFinish.Glow || glowIntensity <= 0f) return Color.black;
			var c = GlowColor;
			return new Color(c.r * glowIntensity, c.g * glowIntensity, c.b * glowIntensity, 1f);
		}
	}

	public void Initialize(string name, Color color)
	{
		Initialize(name, color, PC.Component.FilamentFinish.Glossy, 0.85f, Color.black, 0f);
	}

	public void Initialize(string name, Color color, PC.Component.FilamentFinish finishType,
		float surfaceSmoothness, Color glowColor, float emissionIntensity)
	{
		finish = finishType;
		smoothness = Mathf.Clamp01(surfaceSmoothness);
		glowHex = ColorUtility.ToHtmlStringRGB(glowColor);
		glowIntensity = Mathf.Max(0f, emissionIntensity);
		modelName = string.IsNullOrEmpty(name) ? "Printed Model" : name;
		colorHex = ColorUtility.ToHtmlStringRGB(color);
		spawnId = PrintedSpawnId;
		Info = modelName;
		gameObject.name = modelName;
		var rb = GetComponent<Rigidbody>();
		if (rb != null) savedMass = rb.mass;
		if (Id == 0 && Main.Instance != null) Main.Instance.GetNewId(this);
	}

	public override string GetInfo()
	{
		return string.IsNullOrEmpty(modelName) ? base.GetInfo() : modelName;
	}

	public override void ToData(JObject jObject)
	{
		base.ToData(jObject);
		jObject["printedName"] = modelName;
		jObject["printedColor"] = colorHex;
		jObject["printedFinish"] = (int)finish;
		jObject["printedSmoothness"] = smoothness;
		jObject["printedGlowColor"] = glowHex;
		jObject["printedGlowIntensity"] = glowIntensity;
		jObject["printedScale"] = ToJson(transform.localScale);
		var rb = GetComponent<Rigidbody>();
		jObject["printedMass"] = rb != null ? rb.mass : savedMass;

		var parts = new JArray();
		var filters = GetComponentsInChildren<MeshFilter>(true);
		for (int i = 0; i < filters.Length; i++)
		{
			var mesh = filters[i] != null ? filters[i].sharedMesh : null;
			if (mesh == null) continue;
			var part = new JObject
			{
				["name"] = filters[i].name,
				["pos"] = ToJson(filters[i].transform.localPosition),
				["rot"] = ToJson(filters[i].transform.localRotation),
				["scale"] = ToJson(filters[i].transform.localScale),
				["vertices"] = ToJson(mesh.vertices),
				["triangles"] = ToJson(mesh.triangles)
			};
			var normals = mesh.normals;
			if (normals != null && normals.Length == mesh.vertexCount)
				part["normals"] = ToJson(normals);
			parts.Add(part);
		}
		jObject["printedParts"] = parts;
	}

	public override void FromData(JObject jObject)
	{
		base.FromData(jObject);
		if (jObject.TryGetValue("printedName", out var nameToken) && nameToken != null)
			modelName = nameToken.Value<string>();
		if (jObject.TryGetValue("printedColor", out var colorToken) && colorToken != null)
			colorHex = colorToken.Value<string>();
		if (jObject.TryGetValue("printedScale", out var scaleToken) && scaleToken != null)
			transform.localScale = ReadVector(scaleToken, transform.localScale);
		if (jObject.TryGetValue("printedMass", out var massToken) && massToken != null)
			savedMass = Mathf.Max(0.001f, massToken.Value<float>());
		if (jObject.TryGetValue("printedFinish", out var finishToken) && finishToken != null)
		{
			int raw = finishToken.Value<int>();
			if (raw >= 0 && raw <= 2) finish = (PC.Component.FilamentFinish)raw;
		}
		if (jObject.TryGetValue("printedSmoothness", out var smoothToken) && smoothToken != null)
			smoothness = Mathf.Clamp01(smoothToken.Value<float>());
		if (jObject.TryGetValue("printedGlowColor", out var glowToken) && glowToken != null)
			glowHex = glowToken.Value<string>();
		if (jObject.TryGetValue("printedGlowIntensity", out var glowIntensityToken) && glowIntensityToken != null)
			glowIntensity = Mathf.Max(0f, glowIntensityToken.Value<float>());

		ClearGeneratedVisuals();
		if (jObject.TryGetValue("printedParts", out var partsToken) && partsToken is JArray parts)
		{
			for (int i = 0; i < parts.Count; i++)
			{
				var part = parts[i] as JObject;
				if (part == null) continue;
				var vertices = part.TryGetValue("vertices", out var vt) ? ReadVectors(vt) : null;
				var triangles = part.TryGetValue("triangles", out var tt) ? ReadInts(tt) : null;
				if (vertices == null || triangles == null || vertices.Length == 0 || triangles.Length < 3) continue;

				var go = new GameObject(part.TryGetValue("name", out var nt) && nt != null ? nt.Value<string>() : "Part_" + i);
				go.transform.SetParent(transform, false);
				if (part.TryGetValue("pos", out var pt)) go.transform.localPosition = ReadVector(pt, Vector3.zero);
				if (part.TryGetValue("rot", out var rt)) go.transform.localRotation = ReadQuaternion(rt, Quaternion.identity);
				if (part.TryGetValue("scale", out var st)) go.transform.localScale = ReadVector(st, Vector3.one);

				var mesh = new Mesh { name = modelName + "_" + i };
				mesh.vertices = vertices;
				mesh.triangles = triangles;
				if (part.TryGetValue("normals", out var normalToken) && normalToken != null)
				{
					var normals = ReadVectors(normalToken);
					if (normals != null && normals.Length == vertices.Length) mesh.normals = normals;
					else mesh.RecalculateNormals();
				}
				else mesh.RecalculateNormals();
				mesh.RecalculateBounds();

				var filter = go.AddComponent<MeshFilter>();
				filter.sharedMesh = mesh;
				var renderer = go.AddComponent<MeshRenderer>();
				renderer.sharedMaterial = CreateMaterial();
			}
		}

		Color color = Color.white;
		if (!string.IsNullOrEmpty(colorHex)) ColorUtility.TryParseHtmlString("#" + colorHex, out color);
		var model = PrintableModel.FromInstance(gameObject, modelName);
		model.ApplyColor(color, smoothness, EmissionColor);
		model.AddFinishedPhysics(180f, 0.02f, 5f);
		var rb = GetComponent<Rigidbody>();
		if (rb != null) rb.mass = savedMass;

		spawnId = PrintedSpawnId;
		Info = modelName;
		gameObject.name = modelName;
	}

	// Newtonsoft reflects over every public property, and Vector3.normalized
	// returns another Vector3, so serializing a Unity struct directly throws
	// "Self referencing loop detected for property 'normalized'". Unity values
	// are therefore stored as plain number arrays.
	private static JArray ToJson(Vector3 value)
	{
		return new JArray(value.x, value.y, value.z);
	}

	private static JArray ToJson(Quaternion value)
	{
		return new JArray(value.x, value.y, value.z, value.w);
	}

	private static JArray ToJson(Vector3[] values)
	{
		var array = new JArray();
		if (values == null) return array;
		for (int i = 0; i < values.Length; i++) array.Add(ToJson(values[i]));
		return array;
	}

	private static JArray ToJson(int[] values)
	{
		var array = new JArray();
		if (values == null) return array;
		for (int i = 0; i < values.Length; i++) array.Add(values[i]);
		return array;
	}

	private static Vector3 ReadVector(JToken token, Vector3 fallback)
	{
		if (token is JArray array && array.Count >= 3)
			return new Vector3(array[0].Value<float>(), array[1].Value<float>(), array[2].Value<float>());
		if (token is JObject obj && obj["x"] != null)
			return new Vector3(obj["x"].Value<float>(), obj["y"].Value<float>(), obj["z"].Value<float>());
		return fallback;
	}

	private static Quaternion ReadQuaternion(JToken token, Quaternion fallback)
	{
		if (token is JArray array && array.Count >= 4)
			return new Quaternion(array[0].Value<float>(), array[1].Value<float>(), array[2].Value<float>(), array[3].Value<float>());
		if (token is JObject obj && obj["x"] != null)
			return new Quaternion(obj["x"].Value<float>(), obj["y"].Value<float>(), obj["z"].Value<float>(), obj["w"].Value<float>());
		return fallback;
	}

	private static Vector3[] ReadVectors(JToken token)
	{
		if (!(token is JArray array)) return null;
		var values = new Vector3[array.Count];
		for (int i = 0; i < array.Count; i++) values[i] = ReadVector(array[i], Vector3.zero);
		return values;
	}

	private static int[] ReadInts(JToken token)
	{
		if (!(token is JArray array)) return null;
		var values = new int[array.Count];
		for (int i = 0; i < array.Count; i++) values[i] = array[i].Value<int>();
		return values;
	}

	private void ClearGeneratedVisuals()
	{
		var children = new List<Transform>();
		foreach (Transform child in transform) children.Add(child);
		for (int i = 0; i < children.Count; i++)
			if (children[i] != null) Destroy(children[i].gameObject);
		var filter = GetComponent<MeshFilter>();
		if (filter != null) Destroy(filter);
		var renderer = GetComponent<MeshRenderer>();
		if (renderer != null) Destroy(renderer);
	}

	private static Material CreateMaterial()
	{
		Shader shader = Shader.Find("Universal Render Pipeline/Lit");
		if (shader == null) shader = Shader.Find("Standard");
		if (shader == null) shader = Shader.Find("Diffuse");
		var material = new Material(shader);
		material.color = Color.white;
		return material;
	}
}
