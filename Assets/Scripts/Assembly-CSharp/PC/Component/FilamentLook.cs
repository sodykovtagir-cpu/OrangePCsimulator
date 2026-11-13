using UnityEngine;

namespace PC.Component
{
	public enum FilamentFinish
	{
		Matte = 0,
		Glossy = 1,
		Glow = 2
	}

	/// <summary>Colored plastic, including self-visible (not scene-lighting) glow.
	/// Applies to spools, prints and app previews, including old saved HDR colors.
	/// Imported FBX materials may not support the properties used by Standard/Lit.
	/// Never modify a shared imported material, and update every material slot.
	/// </summary>
	public static class FilamentLook
	{
		public const string GlowLightName = "FilamentGlowLight";
		public const float MaxSurfaceEmission = 0.65f;

		public static bool HasEmission(Color emission)
		{
			return emission.maxColorComponent > 0.001f;
		}

		public static Color SurfaceEmission(Color emission)
		{
			// Preserve hue when loading old 2.4-intensity spools/prints/jobs.
			// Per-channel clamping would turn their yellow-green plastic white.
			float peak = emission.maxColorComponent;
			float scale = peak > MaxSurfaceEmission ? MaxSurfaceEmission / peak : 1f;
			return new Color(Mathf.Max(0f, emission.r) * scale,
				Mathf.Max(0f, emission.g) * scale, Mathf.Max(0f, emission.b) * scale, 1f);
		}

		public static void Apply(Renderer[] renderers, Color color, float smoothness, Color emission)
		{
			if (renderers == null) return;
			emission = SurfaceEmission(emission);
			bool glow = HasEmission(emission);
			// White specular highlights on glossy plastic must not overpower glow.
			float smooth = glow ? Mathf.Min(Mathf.Clamp01(smoothness), 0.25f) : Mathf.Clamp01(smoothness);
			var block = new MaterialPropertyBlock();
			block.SetColor("_Color", color);
			block.SetColor("_BaseColor", color);
			block.SetFloat("_Smoothness", smooth);
			block.SetFloat("_Glossiness", smooth);
			block.SetFloat("_GlossMapScale", smooth);
			block.SetFloat("_Metallic", 0f);
			block.SetColor("_EmissionColor", glow ? emission : Color.black);

			for (int i = 0; i < renderers.Length; i++)
			{
				var renderer = renderers[i];
				if (renderer == null) continue;
				var materials = renderer.materials;
				for (int slot = 0; slot < materials.Length; slot++)
				{
					var material = materials[slot];
					if (material == null) continue;
					bool tintable = material.HasProperty("_Color") || material.HasProperty("_BaseColor");
					if (!tintable || (glow && !material.HasProperty("_EmissionColor")))
					{
						// Match the importer fallback; the project uses built-in Standard.
						var shader = Shader.Find("Standard");
						if (shader != null) material.shader = shader;
					}
					if (material.HasProperty("_Color")) material.SetColor("_Color", color);
					if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
					if (glow) material.EnableKeyword("_EMISSION");
					else material.DisableKeyword("_EMISSION");
					if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", glow ? emission : Color.black);
					// Self-emission only: neither realtime nor baked GI contribution.
					material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
				}
				renderer.SetPropertyBlock(block);
			}
		}

		/// <summary>Clean up legacy runtime light immediately, even with deferred Destroy.</summary>
		public static void RemoveGlowLight(GameObject root)
		{
			if (root == null) return;
			var existing = root.transform.Find(GlowLightName);
			if (existing == null) return;
			var light = existing.GetComponent<Light>();
			if (light != null) light.enabled = false;
			Object.Destroy(existing.gameObject);
		}
	}
}
