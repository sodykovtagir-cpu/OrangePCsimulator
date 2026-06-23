using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;
using PC.Component;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Hardware))]
public class Led : MonoBehaviour, ISave
{
	[Serializable]
	public struct LedAnimation
	{
		[Serializable]
		public struct AnimationPoint
		{
			public FadeType type;

			public Color color;

			public float time;
		}

		public enum FadeType
		{
			None = 0,
			Fade = 1
		}

		public List<AnimationPoint> points;

		public static LedAnimation DefaultAnimation => new LedAnimation
		{
			points = new List<AnimationPoint>
			{
				new AnimationPoint() {type = FadeType.Fade, color = Color.red, time = 1f},
				new AnimationPoint() {type = FadeType.Fade, color = Color.green, time = 1f},
				new AnimationPoint() {type = FadeType.Fade, color = Color.blue, time = 1f},
			}
		};
	}

	[SerializeField]
	private Renderer lightRenderer;

	public LedAnimation animations;

	private Material mat;

	private Color oldColor;

	private void Start()
	{
		var hw = GetComponent<Hardware>();
		if (hw != null) hw.PowerChanged += UpdatePower;
		if (lightRenderer != null) mat = lightRenderer.material;
	}

	private void UpdatePower(bool on)
	{
		if (on)
		{
			StartCoroutine("ColorFade");
			if (mat != null) mat.EnableKeyword("_EMISSION");
			return;
		}
		StopCoroutine("ColorFade");
		if (mat != null) mat.DisableKeyword("_EMISSION");
	}

	public void ChangedAnimation(Led.LedAnimation animations)
	{
		StopCoroutine("ColorFade");
		this.animations.points = animations.points;
		StartCoroutine("ColorFade");
	}

	private IEnumerator ColorFade()
	{
		var pts = animations.points;
		if (pts == null || pts.Count == 0) yield break;
		if (pts.Count == 1)
		{
			if (mat != null) mat.SetColor("_EmissionColor", pts[0].color);
			yield break;
		}

		var first = pts[0];
		oldColor = first.color;
		if (mat != null) mat.SetColor("_EmissionColor", oldColor);

		float time = 0f;
		int i = 1;

		while (true)
		{
			var p = pts[i];

			if (p.type == 0)
			{
				yield return new WaitForSeconds(p.time);
				if (mat != null) mat.SetColor("_EmissionColor", p.color);
				if (mat != null) oldColor = mat.GetColor("_EmissionColor");
				time = 0f;
				i++;
				if (i >= pts.Count) i = 0;
				continue;
			}

			while (time < 1f)
			{
				time += Time.deltaTime / p.time;
				float t = Mathf.Clamp01(time);
				var c = new Color(
					oldColor.r + (p.color.r - oldColor.r) * t,
					oldColor.g + (p.color.g - oldColor.g) * t,
					oldColor.b + (p.color.b - oldColor.b) * t,
					oldColor.a + (p.color.a - oldColor.a) * t
				);
				if (mat != null) mat.SetColor("_EmissionColor", c);
				yield return null;
			}

			time = 0f;
			if (mat != null) oldColor = mat.GetColor("_EmissionColor");
			i++;
			if (i >= pts.Count) i = 0;
		}
	}

	public void ToData(JObject jObject)
	{
		if (jObject == null) return;

		var arr = new JArray();

		if (animations.points != null)
		{
			foreach (var p in animations.points)
			{
				var pointObj = new JObject
				{
					["type"] = p.type.ToString(),
					["time"] = p.time
				};

				var colorObj = new JObject
				{
					["r"] = p.color.r,
					["g"] = p.color.g,
					["b"] = p.color.b,
					["a"] = p.color.a
				};

				pointObj["color"] = colorObj;

				arr.Add(pointObj);
			}
		}

		var animObj = new JObject
		{
			["points"] = arr
		};

		jObject["animations"] = animObj;
	}

	public void FromData(JObject jObject)
	{
		if (jObject == null) return;

		var token = jObject["animations"];
		if (token == null) return;

		var animObj = token as JObject;
		if (animObj == null) return;

		var pointsToken = animObj["points"];
		if (pointsToken == null) return;

		var list = new List<LedAnimation.AnimationPoint>();

		foreach (var p in pointsToken)
		{
			var obj = p as JObject;
			if (obj == null) continue;

			var typeStr = obj["type"] != null ? obj["type"].ToString() : "None";
			var colorObj = obj["color"] as JObject;
			var timeVal = obj["time"] != null ? obj["time"].ToObject<float>() : 1f;

			float r = colorObj?["r"] != null ? colorObj["r"].ToObject<float>() : 1f;
			float g = colorObj?["g"] != null ? colorObj["g"].ToObject<float>() : 1f;
			float b = colorObj?["b"] != null ? colorObj["b"].ToObject<float>() : 1f;
			float a = colorObj?["a"] != null ? colorObj["a"].ToObject<float>() : 1f;

			list.Add(new LedAnimation.AnimationPoint
			{
				type = (LedAnimation.FadeType)Enum.Parse(typeof(LedAnimation.FadeType), typeStr),
				color = new Color(r, g, b, a),
				time = timeVal
			});
		}

		animations.points = list;
	}
}
