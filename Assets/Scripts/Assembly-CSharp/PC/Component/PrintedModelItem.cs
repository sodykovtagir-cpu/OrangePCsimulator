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

	public string ModelName => modelName;

	public void Initialize(string name, Color color)
	{
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
		jObject["printedScale"] = JToken.FromObject(transform.localScale);
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
				["pos"] = JToken.FromObject(filters[i].transform.localPosition),
				["rot"] = JToken.FromObject(filters[i].transform.localRotation),
				["scale"] = JToken.FromObject(filters[i].transform.localScale),
				["vertices"] = JArray.FromObject(mesh.vertices),
				["triangles"] = JArray.FromObject(mesh.triangles)
			};
			if (mesh.normals != null && mesh.normals.Length == mesh.vertexCount)
				part["normals"] = JArray.FromObject(mesh.normals);
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
			transform.localScale = scaleToken.ToObject<Vector3>();
		if (jObject.TryGetValue("printedMass", out var massToken) && massToken != null)
			savedMass = Mathf.Max(0.001f, massToken.Value<float>());

		ClearGeneratedVisuals();
		if (jObject.TryGetValue("printedParts", out var partsToken) && partsToken is JArray parts)
		{
			for (int i = 0; i < parts.Count; i++)
			{
				var part = parts[i] as JObject;
				if (part == null) continue;
				var vertices = part.TryGetValue("vertices", out var vt) && vt != null ? vt.ToObject<Vector3[]>() : null;
				var triangles = part.TryGetValue("triangles", out var tt) && tt != null ? tt.ToObject<int[]>() : null;
				if (vertices == null || triangles == null || vertices.Length == 0 || triangles.Length < 3) continue;

				var go = new GameObject(part.TryGetValue("name", out var nt) && nt != null ? nt.Value<string>() : "Part_" + i);
				go.transform.SetParent(transform, false);
				if (part.TryGetValue("pos", out var pt) && pt != null) go.transform.localPosition = pt.ToObject<Vector3>();
				if (part.TryGetValue("rot", out var rt) && rt != null) go.transform.localRotation = rt.ToObject<Quaternion>();
				if (part.TryGetValue("scale", out var st) && st != null) go.transform.localScale = st.ToObject<Vector3>();

				var mesh = new Mesh { name = modelName + "_" + i };
				mesh.vertices = vertices;
				mesh.triangles = triangles;
				if (part.TryGetValue("normals", out var normalToken) && normalToken != null)
				{
					var normals = normalToken.ToObject<Vector3[]>();
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
		model.ApplyColor(color);
		model.AddFinishedPhysics(180f, 0.02f, 5f);
		var rb = GetComponent<Rigidbody>();
		if (rb != null) rb.mass = savedMass;

		spawnId = PrintedSpawnId;
		Info = modelName;
		gameObject.name = modelName;
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
