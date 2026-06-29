using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace PC.Component
{
	[RequireComponent(typeof(AudioSource))]
	public class Printer : Device
	{
		[SerializeField]
		private TextureLoader paperPrefab;

		[SerializeField]
		private GameObject paperInput;

		[SerializeField]
		private GameObject indicator;

		public ColorYmck totalInk = new ColorYmck(1, 1, 1, 1);

		public ColorYmck remainingInk = new ColorYmck(1, 1, 1, 1);

		[SerializeField]
		private int remainingPaper;

		[SerializeField]
		private Transform pivot;

        [SerializeField]
        private int maxResolution = 64;

        public int MaxResolution => maxResolution;

        [SerializeField]
        private float printSpeed = 1f;

        [SerializeField]
		private float[] printStepIntervals;

		[SerializeField]
		private Vector3 endPoint;

		[SerializeField]
		private AudioClip refillSound;

		private AudioSource source;

		private Color[] sourceColors;

		private int width;

		private int height;

		private bool grayscale;

		private int copies;

		private int RemainingPaper
		{
			get
			{
				return remainingPaper;
			}
			set
            {
				remainingPaper = value;
				if (paperInput != null)
                {
					paperInput.SetActive(value > 0);
                }
            }
		}

		public bool Busy { get; private set; }

		protected override void Awake()
		{
			base.Awake();
			source = GetComponent<AudioSource>();
		}

		public void PrintPicture(Texture2D picture, bool grayscale, int copies)
		{
			if (Busy) return;

			Busy = true;
			if (indicator != null) indicator.SetActive(true);
			if (picture == null) return;

			sourceColors = picture.GetPixels();
			width = picture.width;
			height = picture.height;
			this.grayscale = grayscale;
			this.copies = copies;

			if (source != null)
			{
				source.time = 0f;
				source.Play();
			}

			StartCoroutine(PrintingAnimation());
		}

		private IEnumerator PrintingAnimation()
		{
			if (printStepIntervals == null || printStepIntervals.Length == 0) yield break;

			yield return new UnityEngine.WaitForSeconds(printStepIntervals[0]);

			while (copies > 0 && remainingPaper > 0)
			{
				var colors = sourceColors != null ? new UnityEngine.Color[sourceColors.Length] : null;

				copies--;
				RemainingPaper = remainingPaper - 1;

				if (source != null)
				{
					source.time = printStepIntervals[0];
					source.Play();
				}

				if (pivot != null) pivot.localPosition = UnityEngine.Vector3.zero;

				byte[] png;
				var tex = PrintAvailableArea(sourceColors, colors, width, height, out png);

				var obj = UnityEngine.Object.Instantiate(paperPrefab, pivot.position, pivot.rotation);
				obj.SetTexture(tex, png);

				var tr = obj.transform;
				var rb = tr != null ? tr.GetComponent<UnityEngine.Rigidbody>() : null;

				if (rb != null)
				{
					rb.isKinematic = true;
					rb.detectCollisions = false;
				}

				if (tr != null && pivot != null) tr.SetParent(pivot);

				int len = printStepIntervals.Length;
				for (int i = 1; i <= len; i++)
				{
					float current = i == len
						? (source != null && source.clip != null ? source.clip.length : printStepIntervals[len - 1])
						: printStepIntervals[i];
					float previous = printStepIntervals[i - 1];
					float interval = current - previous;

					float value = (float)i / len;
					float t = 0f;
					var last = pivot != null ? pivot.localPosition : UnityEngine.Vector3.zero;

					while (t < 1f)
					{
						t += UnityEngine.Time.deltaTime / interval;

						float te = t * 5f;
						if (te > 1f) te = 1f;
						if (te < 0f) te = 0f;

						float v = value;
						if (v > 1f) v = 1f;
						if (v < 0f) v = 0f;

						var target = UnityEngine.Vector3.Lerp(UnityEngine.Vector3.zero, endPoint, v);
						if (pivot != null) pivot.localPosition = UnityEngine.Vector3.Lerp(last, target, te);

						yield return null;
					}
				}

				if (tr != null) tr.SetParent(null);
				if (rb != null)
				{
					rb.isKinematic = false;
					rb.detectCollisions = true;
				}
			}

			if (source != null) source.Stop();
			Busy = false;
			if (indicator != null) indicator.SetActive(false);

			var ach = CloudOnceManager.Instance.GetAchievementFromId("print_master");
			if (ach != null) ach.Increment(1f, null);
		}

		private UnityEngine.Texture2D PrintAvailableArea(UnityEngine.Color[] sourceColors, UnityEngine.Color[] buffer, int width, int height, out byte[] pngData)
		{
			pngData = null;
			if (buffer == null || sourceColors == null) return null;

			var tex = new UnityEngine.Texture2D(width, height, UnityEngine.TextureFormat.RGBA32, false);
			tex.filterMode = UnityEngine.FilterMode.Point;

			int count = buffer.Length;
			for (int i = 0; i < count; i++)
			{
				if (!grayscale)
				{
					var src = sourceColors[i];
					var cmyk = ColorYmck.FromRgb(src.r, src.g, src.b);

					float cUse = remainingInk.c > 0f ? UnityEngine.Mathf.Min(cmyk.c, remainingInk.c) : 0f;
					float mUse = remainingInk.m > 0f ? UnityEngine.Mathf.Min(cmyk.m, remainingInk.m) : 0f;
					float yUse = remainingInk.y > 0f ? UnityEngine.Mathf.Min(cmyk.y, remainingInk.y) : 0f;
					float kUse = remainingInk.k > 0f ? UnityEngine.Mathf.Min(cmyk.k, remainingInk.k) : 0f;

					var outRgb = new ColorYmck { c = cUse, m = mUse, y = yUse, k = kUse }.ToRgb();
					buffer[i] = new UnityEngine.Color(outRgb.r, outRgb.g, outRgb.b, outRgb.a);

					float inv = 1f / count;
					remainingInk.c = UnityEngine.Mathf.Max(0f, remainingInk.c - cmyk.c * inv);
					remainingInk.m = UnityEngine.Mathf.Max(0f, remainingInk.m - cmyk.m * inv);
					remainingInk.y = UnityEngine.Mathf.Max(0f, remainingInk.y - cmyk.y * inv);
					remainingInk.k = UnityEngine.Mathf.Max(0f, remainingInk.k - cmyk.k * inv);
				}
				else
				{
					if (remainingInk.k <= 0f)
					{
						buffer[i] = new UnityEngine.Color(1f, 1f, 1f, 1f);
					}
					else
					{
						var src = sourceColors[i];
						float lum = src.r * 0.299f + src.g * 0.587f + src.b * 0.114f;
						remainingInk.k = UnityEngine.Mathf.Max(0f, remainingInk.k - (1f - lum) / count);
						buffer[i] = new UnityEngine.Color(lum, lum, lum, 1f);
					}
				}
			}

			tex.SetPixels(buffer);
			pngData = UnityEngine.ImageConversion.EncodeToPNG(tex);
			tex.Apply(false, true);
			return tex;
		}

		private void OnCollisionEnter(UnityEngine.Collision collision)
		{
			if (collision == null) return;
			var col = collision.collider;
			if (col == null) return;

			if (col.CompareTag("Ink"))
			{
				var ink = col.GetComponent<PrinterInk>();
				if (ink != null)
				{
					if (ink.y) remainingInk.y = totalInk.y;
					if (ink.m) remainingInk.m = totalInk.m;
					if (ink.c) remainingInk.c = totalInk.c;
					if (ink.k) remainingInk.k = totalInk.k;
					if (source != null) source.PlayOneShot(refillSound);
					UnityEngine.Object.Destroy(col.gameObject);
				}
				return;
			}

			if (col.CompareTag("Paper"))
			{
				var loader = col.GetComponent<TextureLoader>();
				if (loader != null && loader.IsEmpty())
				{
					RemainingPaper = remainingPaper + 1;
					UnityEngine.Object.Destroy(collision.gameObject);
				}
			}
		}

		private void CalculateInkUsage(UnityEngine.Texture2D texture)
		{
			if (texture == null) return;
			var pixels = texture.GetPixels();
			if (pixels == null || pixels.Length == 0) return;

			float r = 0f, g = 0f, b = 0f;
			int n = pixels.Length;
			for (int i = 0; i < n; i++)
			{
				var c = pixels[i];
				r += c.r;
				g += c.g;
				b += c.b;
			}

			r /= n;
			g /= n;
			b /= n;

			ColorYmck.FromRgb(r, g, b);
		}

		public override void ToData(JObject jObject)
		{
			base.ToData(jObject);
			jObject["y"] = remainingInk.y;
			jObject["m"] = remainingInk.m;
			jObject["c"] = remainingInk.c;
			jObject["k"] = remainingInk.k;
			jObject["paper"] = remainingPaper;
		}

		public override void FromData(JObject jObject)
		{
			base.FromData(jObject);
			if (jObject == null) return;

			var yTok = jObject["y"];
			var mTok = jObject["m"];
			var cTok = jObject["c"];
			var kTok = jObject["k"];
			var pTok = jObject["paper"];

			if (yTok != null) remainingInk.y = yTok.Value<float>();
			if (mTok != null) remainingInk.m = mTok.Value<float>();
			if (cTok != null) remainingInk.c = cTok.Value<float>();
			if (kTok != null) remainingInk.k = kTok.Value<float>();
			if (pTok != null) RemainingPaper = pTok.Value<int>();
		}
	}
}
