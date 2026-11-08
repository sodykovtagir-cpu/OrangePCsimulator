using UnityEngine;

namespace PC.Component
{
	/// <summary>
	/// Runtime-imported mesh that can be sent to a 3D printer.
	/// The importer owns the GameObject; the printer only animates a wrapper.
	/// </summary>
	public sealed class PrintableModel
	{
		public string Name;
		public GameObject Instance;
		public int TriangleCount;
		public Bounds Bounds;
		public float BoundsVolume;

		public static PrintableModel FromInstance(GameObject instance, string name)
		{
			var model = new PrintableModel
			{
				Name = string.IsNullOrEmpty(name) ? "Model" : name,
				Instance = instance,
				TriangleCount = 0,
				Bounds = new Bounds(Vector3.zero, Vector3.one * 0.1f),
				BoundsVolume = 0f
			};

			if (instance == null) return model;

			bool hasBounds = false;
			var filters = instance.GetComponentsInChildren<MeshFilter>(true);
			for (int i = 0; i < filters.Length; i++)
			{
				var mesh = filters[i] != null ? filters[i].sharedMesh : null;
				if (mesh == null) continue;
				model.TriangleCount += mesh.triangles.Length / 3;
			}

			var renderers = instance.GetComponentsInChildren<Renderer>(true);
			for (int i = 0; i < renderers.Length; i++)
			{
				if (renderers[i] == null) continue;
				if (!hasBounds)
				{
					model.Bounds = renderers[i].bounds;
					hasBounds = true;
				}
				else model.Bounds.Encapsulate(renderers[i].bounds);
			}

			var size = model.Bounds.size;
			model.BoundsVolume = Mathf.Max(0f, size.x) * Mathf.Max(0f, size.y) * Mathf.Max(0f, size.z);
			return model;
		}

		public void DestroyInstance()
		{
			if (Instance != null) UnityEngine.Object.Destroy(Instance);
			Instance = null;
		}

		public void ApplyColor(Color color)
		{
			if (Instance == null) return;
			var block = new MaterialPropertyBlock();
			block.SetColor("_Color", color);
			block.SetColor("_BaseColor", color);
			var renderers = Instance.GetComponentsInChildren<Renderer>(true);
			for (int i = 0; i < renderers.Length; i++)
				if (renderers[i] != null) renderers[i].SetPropertyBlock(block);
		}
	}
}
