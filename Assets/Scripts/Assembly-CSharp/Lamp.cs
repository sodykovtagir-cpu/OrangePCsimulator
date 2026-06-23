using System;
using UnityEngine;

public class Lamp : MonoBehaviour
{
	[Serializable]
	private class ChangeMat
	{
		public Renderer renderer;

		public int index;
	}

	[SerializeField]
	private Color offColor;

	[SerializeField]
	private Texture2D offTexture;

	[SerializeField]
	private Renderer[] lamps;

	[SerializeField]
	private ReflectionProbe reflection;

	[SerializeField]
	private ChangeMat[] mats;

	[SerializeField]
	private Material onMat;

	[SerializeField]
	private Material offMat;

	private LightmapData[] oldLightmaps;

	private Color oldSky;

	private Color oldEquator;

	private Color oldGround;

	private bool isOn;

	public bool IsOn
	{
		get
		{
			return isOn;
		}
		set
        {
            isOn = value;
			if (lamps != null)
			{
				foreach (var r in lamps)
				{
					if (!r) continue;
					var matsArray = r.materials;
					if (matsArray == null || matsArray.Length == 0) continue;

					var m = matsArray[matsArray.Length - 1];
					if (m == null) continue;

					if (value) m.EnableKeyword("_EMISSION");
					else       m.DisableKeyword("_EMISSION");
				}
			}
			if (mats != null)
			{
				foreach (var cm in mats)
				{
					if (cm == null || cm.renderer == null) continue;

					var ms = cm.renderer.materials;
					if (ms == null || cm.index < 0 || cm.index >= ms.Length) continue;

					ms[cm.index] = value ? onMat : offMat;
					cm.renderer.materials = ms;
				}
			}

			if (value)
			{
				RenderSettings.ambientSkyColor = oldSky;
				RenderSettings.ambientEquatorColor = oldEquator;
				RenderSettings.ambientGroundColor = oldGround;

				if (oldLightmaps != null)
					LightmapSettings.lightmaps = oldLightmaps;

				if (reflection) reflection.enabled = true;
			}
			else
			{
				RenderSettings.ambientSkyColor = offColor;
				RenderSettings.ambientEquatorColor = offColor;
				RenderSettings.ambientGroundColor = offColor;

				if (offTexture != null)
				{
					LightmapSettings.lightmaps = new[]
					{
						new LightmapData { lightmapColor = offTexture }
					};
				}

				if (reflection) reflection.enabled = false;
			}
        }
	}

	private void Awake()
	{
		oldLightmaps = LightmapSettings.lightmaps;
		oldSky = RenderSettings.ambientSkyColor;
		oldEquator = RenderSettings.ambientEquatorColor;
		oldGround = RenderSettings.ambientGroundColor;
	}
}
