using System.Collections.Generic;
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

		// ----- Real geometry --------------------------------------------------
		// Renderer.bounds is empty while the imported model is inactive, and the
		// axis-aligned box hugely overestimates plastic for anything hollow, so
		// the printer measures the actual triangles instead.

		private float cachedSolidVolume = -1f;
		private float cachedSurfaceArea;
		private Vector3 cachedGeometryScale = new Vector3(float.NaN, 0f, 0f);

		/// <summary>Solid volume of the meshes in world units³ (signed tetrahedra).</summary>
		public float GetWorldSolidVolume()
		{
			EnsureGeometryCache();
			return cachedSolidVolume;
		}

		/// <summary>Surface area of the meshes in world units².</summary>
		public float GetWorldSurfaceArea()
		{
			EnsureGeometryCache();
			return cachedSurfaceArea;
		}

		public void InvalidateGeometryCache()
		{
			cachedSolidVolume = -1f;
		}

		private void EnsureGeometryCache()
		{
			if (Instance == null)
			{
				cachedSolidVolume = 0f;
				cachedSurfaceArea = 0f;
				return;
			}
			Vector3 scale = Instance.transform.lossyScale;
			if (cachedSolidVolume >= 0f && (scale - cachedGeometryScale).sqrMagnitude < 0.0000001f) return;
			cachedGeometryScale = scale;

			float volume = 0f;
			float area = 0f;
			var filters = Instance.GetComponentsInChildren<MeshFilter>(true);
			for (int i = 0; i < filters.Length; i++)
			{
				var mesh = filters[i] != null ? filters[i].sharedMesh : null;
				if (mesh == null) continue;
				var vertices = mesh.vertices;
				var triangles = mesh.triangles;
				Matrix4x4 toWorld = filters[i].transform.localToWorldMatrix;
				float signed = 0f;
				for (int t = 0; t + 2 < triangles.Length; t += 3)
				{
					Vector3 a = toWorld.MultiplyPoint3x4(vertices[triangles[t]]);
					Vector3 b = toWorld.MultiplyPoint3x4(vertices[triangles[t + 1]]);
					Vector3 c = toWorld.MultiplyPoint3x4(vertices[triangles[t + 2]]);
					signed += Vector3.Dot(a, Vector3.Cross(b, c)) / 6f;
					area += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
				}
				volume += Mathf.Abs(signed);
			}
			cachedSolidVolume = volume;
			cachedSurfaceArea = area;
		}

		/// <summary>
		/// World-space size measured from the meshes, so it also works while the
		/// imported instance is still disabled (pending print preview).
		/// </summary>
		public Vector3 GetWorldBoundsSize()
		{
			if (Instance == null) return Vector3.zero;
			var filters = Instance.GetComponentsInChildren<MeshFilter>(true);
			Vector3 min = Vector3.zero, max = Vector3.zero;
			bool has = false;
			for (int i = 0; i < filters.Length; i++)
			{
				var mesh = filters[i] != null ? filters[i].sharedMesh : null;
				if (mesh == null) continue;
				Matrix4x4 toWorld = filters[i].transform.localToWorldMatrix;
				Bounds local = mesh.bounds;
				Vector3 center = local.center;
				Vector3 extents = local.extents;
				for (int corner = 0; corner < 8; corner++)
				{
					Vector3 point = center + Vector3.Scale(extents, new Vector3(
						(corner & 1) == 0 ? -1f : 1f,
						(corner & 2) == 0 ? -1f : 1f,
						(corner & 4) == 0 ? -1f : 1f));
					point = toWorld.MultiplyPoint3x4(point);
					if (!has) { min = point; max = point; has = true; }
					else { min = Vector3.Min(min, point); max = Vector3.Max(max, point); }
				}
			}
			return has ? max - min : Vector3.zero;
		}

		public void ApplyColor(Color color)
		{
			ApplyColor(color, 0.6f, Color.black);
		}

		/// <summary>
		/// Recolor a print and match the plastic it was printed from: matte or
		/// glossy smoothness, plus emission for glow-in-the-dark filament.
		/// </summary>
		public void ApplyColor(Color color, float smoothness, Color emission)
		{
			if (Instance == null) return;
			var renderers = Instance.GetComponentsInChildren<Renderer>(true);
			FilamentLook.Apply(renderers, color, smoothness, emission);
			if (FilamentLook.HasEmission(emission))
				FilamentLook.EnsureGlowLight(Instance, color, 0.6f, 1.2f);
		}

		/// <summary>
		/// Give the finished print a Rigidbody and one BoxCollider per disconnected
		/// mesh island (e.g. chair seat / legs / back get separate colliders).
		/// Colliders are added on child objects so the visual mesh is untouched.
		/// </summary>
		public void AddFinishedPhysics()
		{
			AddFinishedPhysics(180f, 0.02f, 5f);
		}

		public void AddFinishedPhysics(float densityKgPerCubicMeter, float minMassKg, float maxMassKg)
		{
			AddFinishedPhysics(densityKgPerCubicMeter, minMassKg, maxMassKg, 0f);
		}

		/// <param name="measuredMassKg">
		/// Mass already measured by the printer from the extruded plastic volume.
		/// When positive it wins over the bounding-box estimate, which is only a
		/// fallback for models restored without a print job.
		/// </param>
		public void AddFinishedPhysics(float densityKgPerCubicMeter, float minMassKg, float maxMassKg, float measuredMassKg)
		{
			if (Instance == null) return;
			GameObject root = Instance;

			var rb = root.GetComponent<Rigidbody>();
			if (rb == null) rb = root.AddComponent<Rigidbody>();
			rb.isKinematic = false;
			rb.useGravity = true;
			// Printed plastic is mostly infill/air; use the actual finished world
			// size, not the original import bounds, so tiny prints stay light.
			float volume = GetWorldBoundsVolume(root);
			float density = Mathf.Max(0f, densityKgPerCubicMeter);
			float minMass = Mathf.Max(0.001f, minMassKg);
			float maxMass = Mathf.Max(minMass, maxMassKg);
			float mass = measuredMassKg > 0f ? measuredMassKg : volume * density;
			rb.mass = Mathf.Clamp(mass, minMass, maxMass);
			rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
			rb.interpolation = RigidbodyInterpolation.Interpolate;

			bool added = false;
			var filters = root.GetComponentsInChildren<MeshFilter>(true);
			for (int f = 0; f < filters.Length; f++)
			{
				var filter = filters[f];
				var mesh = filter != null ? filter.sharedMesh : null;
				if (mesh == null) continue;
				added |= AddIslandColliders(root, filter, mesh);
			}

			if (!added)
			{
				// Fallback for skinned / renderer-only models: one box per renderer.
				var renderers = root.GetComponentsInChildren<Renderer>(true);
				int index = 0;
				for (int r = 0; r < renderers.Length; r++)
				{
					if (renderers[r] == null) continue;
					Bounds local = WorldBoundsToLocal(root.transform, renderers[r].bounds);
					if (local.size.sqrMagnitude < 0.000001f) continue;
					CreateBox(root, "PrintCollider_" + index++, local);
					added = true;
				}
			}

			if (!added)
			{
				Bounds local = WorldBoundsToLocal(root.transform, new Bounds(root.transform.position, Vector3.one * 0.1f));
				CreateBox(root, "PrintCollider_0", local);
			}
		}

		private static bool AddIslandColliders(GameObject root, MeshFilter filter, Mesh mesh)
		{
			Vector3[] vertices = mesh.vertices;
			int[] triangles = mesh.triangles;
			if (vertices.Length == 0 || triangles.Length < 3) return false;

			Matrix4x4 toRoot = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
			var localVerts = new Vector3[vertices.Length];
			var weldIds = new int[vertices.Length];
			var weldMap = new Dictionary<Vector3, int>();
			int weldCount = 0;
			for (int i = 0; i < vertices.Length; i++)
			{
				Vector3 p = toRoot.MultiplyPoint3x4(vertices[i]);
				localVerts[i] = p;
				Vector3 key = new Vector3(
					Mathf.Round(p.x * 10000f) / 10000f,
					Mathf.Round(p.y * 10000f) / 10000f,
					Mathf.Round(p.z * 10000f) / 10000f);
				if (!weldMap.TryGetValue(key, out int id))
				{
					id = weldCount++;
					weldMap[key] = id;
				}
				weldIds[i] = id;
			}

			var parent = new int[weldCount];
			for (int i = 0; i < weldCount; i++) parent[i] = i;
			System.Func<int, int> find = null;
			find = x =>
			{
				while (parent[x] != x)
				{
					parent[x] = parent[parent[x]];
					x = parent[x];
				}
				return x;
			};
			System.Action<int, int> union = (a, b) =>
			{
				int ra = find(a), rb2 = find(b);
				if (ra != rb2) parent[ra] = rb2;
			};

			for (int t = 0; t + 2 < triangles.Length; t += 3)
			{
				int a = weldIds[triangles[t]];
				int b = weldIds[triangles[t + 1]];
				int c = weldIds[triangles[t + 2]];
				union(a, b);
				union(a, c);
			}

			var islandBounds = new Dictionary<int, Bounds>();
			for (int i = 0; i < localVerts.Length; i++)
			{
				int island = find(weldIds[i]);
				if (!islandBounds.TryGetValue(island, out Bounds b))
					islandBounds[island] = new Bounds(localVerts[i], Vector3.zero);
				else
				{
					b.Encapsulate(localVerts[i]);
					islandBounds[island] = b;
				}
			}

			bool added = false;
			int index = 0;
			foreach (var kv in islandBounds)
			{
				Bounds b = kv.Value;
				if (b.size.sqrMagnitude < 0.000001f) continue;
				CreateBox(root, "PrintCollider_" + filter.name + "_" + index++, b);
				added = true;
			}
			return added;
		}

		private float GetWorldBoundsVolume(GameObject root)
		{
			var renderers = root.GetComponentsInChildren<Renderer>(true);
			Bounds bounds = new Bounds(root.transform.position, Vector3.zero);
			bool has = false;
			for (int i = 0; i < renderers.Length; i++)
			{
				if (renderers[i] == null) continue;
				if (!has) { bounds = renderers[i].bounds; has = true; }
				else bounds.Encapsulate(renderers[i].bounds);
			}
			if (!has) return Mathf.Max(0f, BoundsVolume);
			Vector3 s = bounds.size;
			return Mathf.Max(0f, s.x) * Mathf.Max(0f, s.y) * Mathf.Max(0f, s.z);
		}

		private static Bounds WorldBoundsToLocal(Transform root, Bounds world)
		{
			Bounds local = new Bounds(Vector3.zero, Vector3.zero);
			bool has = false;
			for (int c = 0; c < 8; c++)
			{
				Vector3 corner = world.center + Vector3.Scale(world.extents, new Vector3(
					(c & 1) == 0 ? -1f : 1f,
					(c & 2) == 0 ? -1f : 1f,
					(c & 4) == 0 ? -1f : 1f));
				Vector3 p = root.InverseTransformPoint(corner);
				if (!has) { local = new Bounds(p, Vector3.zero); has = true; }
				else local.Encapsulate(p);
			}
			return local;
		}

		private static void CreateBox(GameObject root, string name, Bounds localBounds)
		{
			var go = new GameObject(name);
			go.transform.SetParent(root.transform, false);
			go.transform.localPosition = Vector3.zero;
			go.transform.localRotation = Quaternion.identity;
			go.transform.localScale = Vector3.one;
			var box = go.AddComponent<BoxCollider>();
			box.center = localBounds.center;
			box.size = localBounds.size;
			box.isTrigger = false;
		}
	}
}
