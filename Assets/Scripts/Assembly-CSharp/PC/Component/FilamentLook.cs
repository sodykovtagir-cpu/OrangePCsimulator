using UnityEngine;

namespace PC.Component
{
	/// <summary>Surface type of a filament plastic.</summary>
	public enum FilamentFinish
	{
		Matte = 0,
		Glossy = 1,
		Glow = 2
	}

	/// <summary>
	/// Shared material helpers for filament spools and printed models:
	/// matte / glossy smoothness and glow-in-the-dark emission.
	/// Recoloring stays on MaterialPropertyBlock so shared materials are never
	/// edited; only glowing plastic needs its own material instance, because a
	/// property block cannot enable the _EMISSION shader keyword.
	/// </summary>
	public static class FilamentLook
	{
		public const string GlowLightName = "FilamentGlowLight";

		public static bool HasEmission(Color emission)
		{
			return emission.maxColorComponent > 0.001f;
		}

		public static void Apply(Renderer[] renderers, Color color, float smoothness, Color emission)
		{
			if (renderers == null) return;
			var block = new MaterialPropertyBlock();
			block.SetColor("_Color", color);
			block.SetColor("_BaseColor", color);
			float smooth = Mathf.Clamp01(smoothness);
			block.SetFloat("_Smoothness", smooth);
			block.SetFloat("_Glossiness", smooth);
			block.SetFloat("_GlossMapScale", smooth);
			block.SetFloat("_Metallic", 0f);
			bool glow = HasEmission(emission);
			block.SetColor("_EmissionColor", glow ? emission : Color.black);

			for (int i = 0; i < renderers.Length; i++)
			{
				var renderer = renderers[i];
				if (renderer == null) continue;
				if (glow) MakeEmissive(renderer, emission);
				renderer.SetPropertyBlock(block);
			}
		}

		private static void MakeEmissive(Renderer renderer, Color emission)
		{
			var material = renderer.material;
			if (material == null) return;
			material.EnableKeyword("_EMISSION");
			if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", emission);
			material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
		}

		/// <summary>
		/// Glow plastic keeps lighting its surroundings in the dark. The light is
		/// a named child so it is reused instead of duplicated on every recolor.
		/// </summary>
		public static void EnsureGlowLight(GameObject root, Color color, float intensity, float range)
		{
			if (root == null) return;
			var existing = root.transform.Find(GlowLightName);
			var light = existing != null ? existing.GetComponent<Light>() : null;
			if (light == null)
			{
				var go = new GameObject(GlowLightName);
				go.transform.SetParent(root.transform, false);
				go.transform.localPosition = Vector3.zero;
				light = go.AddComponent<Light>();
			}
			light.type = LightType.Point;
			light.color = new Color(color.r, color.g, color.b, 1f);
			light.intensity = Mathf.Max(0f, intensity);
			light.range = Mathf.Max(0.05f, range);
			light.shadows = LightShadows.None;
			light.enabled = intensity > 0f;
		}

		public static void RemoveGlowLight(GameObject root)
		{
			if (root == null) return;
			var existing = root.transform.Find(GlowLightName);
			if (existing != null) Object.Destroy(existing.gameObject);
		}
	}
}
