using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
	public class ColorSettings : App
	{
		[SerializeField]
		private CanvasGroup activeGroup;

		[SerializeField]
		private GameObject example;

		[SerializeField]
		private GameObject advanced;

		[SerializeField]
		private GameObject itemPrefab;

		[SerializeField]
		private GameObject blockPrefab;

		[SerializeField]
		private Transform itemParent;

		[SerializeField]
		private Transform blockParent;

		[SerializeField]
		private int maxBlock = 10;

		[SerializeField]
		private Image colorPreview;

		[SerializeField]
		private Text timePreview;

		[SerializeField]
		private Text typePreview;

		[SerializeField]
		private Sprite sprite_none;

		[SerializeField]
		private Sprite sprite_fade;

		private Led.LedAnimation.AnimationPoint animationPreview = new Led.LedAnimation.AnimationPoint() { color = new Color(1f, 1f, 1f, 1f), type = Led.LedAnimation.FadeType.Fade, time = 1f };

		private Button[] itemBlocks;

		private GameObject[] colorBlocks;

		private Led[] leds;

		private int selected;

		protected override void Start()
		{
			base.Start();

			leds = GetLed();
			if (activeGroup != null) activeGroup.interactable = leds != null && leds.Length > 0;

			if (leds != null)
			{
				itemBlocks = new UnityEngine.UI.Button[leds.Length];
				colorBlocks = new UnityEngine.GameObject[maxBlock];

				for (int i = 0; i < leds.Length; i++)
				{
					var go = UnityEngine.Object.Instantiate(itemPrefab, itemParent);
					var btn = go != null ? go.GetComponent<UnityEngine.UI.Button>() : null;
					itemBlocks[i] = btn;

					var txt = go != null ? go.GetComponentInChildren<UnityEngine.UI.Text>() : null;
					if (txt != null) txt.text = i.ToString();

					int idx = i;
					if (btn != null) btn.onClick.AddListener(() => OtherLed(idx));
				}

				for (int i = 0; i < maxBlock; i++)
				{
					var block = Instantiate(blockPrefab, blockParent);
					colorBlocks[i] = block;

					var btn = block != null && block.transform.childCount > 0
						? block.transform.GetChild(0).GetComponent<UnityEngine.UI.Button>()
						: null;

					int idx = i;
					if (btn != null) btn.onClick.AddListener(() => RemoveAnimation(idx));
				}
			}

			var timeLabel = Localization.GetText("Time");
			var typeLabel = Localization.GetText("Type");
			if (timePreview != null) timePreview.text = timeLabel + ": " + animationPreview.time.ToString("0.#") + "s";
			if (typePreview != null) typePreview.text = typeLabel + ": " + ((Led.LedAnimation.FadeType)animationPreview.type).ToString();

			if (example != null) example.SetActive(true);
			if (advanced != null) advanced.SetActive(false);

			if (leds != null && leds.Length > 0)
			{
				OtherLed(0);
				return;
			}

			if (colorBlocks != null)
			{
				for (int i = 0; i < colorBlocks.Length; i++)
				{
					var go = colorBlocks[i];
					if (go != null) go.SetActive(false);
				}
			}
		}

		public void AdvancedMode()
		{
			if (example != null) example.SetActive(false);
			if (advanced != null) advanced.SetActive(true);
		}

		public void ChangeAnimation(int i)
		{
			if (leds != null)
			{
				if (i == 0)
				{
					for (int k = 0; k < leds.Length; k++)
					{
						var led = leds[k];
						if (led != null) led.ChangedAnimation(Led.LedAnimation.DefaultAnimation);
					}
				}
				else if (i == 1)
				{
					for (int k = 0; k < leds.Length; k++)
					{
						var led = leds[k];
						if (led == null) continue;
						var anim = new Led.LedAnimation { points = new List<Led.LedAnimation.AnimationPoint>() };
						anim.points.Add(new Led.LedAnimation.AnimationPoint() {type = Led.LedAnimation.FadeType.None, color = Color.red, time = 1f});
						anim.points.Add(new Led.LedAnimation.AnimationPoint() {type = Led.LedAnimation.FadeType.None, color = Color.green, time = 1f});
						anim.points.Add(new Led.LedAnimation.AnimationPoint() {type = Led.LedAnimation.FadeType.None, color = Color.blue, time = 1f});
						led.ChangedAnimation(anim);
					}
				}
				else if (i == 2)
				{
					for (int k = 0; k < leds.Length; k++)
					{
						var led = leds[k];
						if (led == null) continue;
						var anim = new Led.LedAnimation { points = new List<Led.LedAnimation.AnimationPoint>() };
						anim.points.Add(new Led.LedAnimation.AnimationPoint() {type = Led.LedAnimation.FadeType.None, color = Color.red, time = 0.2f});
						anim.points.Add(new Led.LedAnimation.AnimationPoint() {type = Led.LedAnimation.FadeType.None, color = Color.green, time = 0.2f});
						anim.points.Add(new Led.LedAnimation.AnimationPoint() {type = Led.LedAnimation.FadeType.None, color = Color.blue, time = 0.2f});
						anim.points.Add(new Led.LedAnimation.AnimationPoint() {type = Led.LedAnimation.FadeType.None, color = Color.yellow, time = 0.2f});
						anim.points.Add(new Led.LedAnimation.AnimationPoint() {type = Led.LedAnimation.FadeType.None, color = Color.cyan, time = 0.2f});
						anim.points.Add(new Led.LedAnimation.AnimationPoint() {type = Led.LedAnimation.FadeType.None, color = Color.magenta, time = 0.2f});
						led.ChangedAnimation(anim);
					}
				}
			}
			UpdateBlock();
		}

		public void OnSliderChangedRed(float value)
		{
			animationPreview.color.r = value;
			var img = colorPreview;
			if (img != null) img.color = new Color(animationPreview.color.r, animationPreview.color.g, animationPreview.color.b, animationPreview.color.a);
		}

		public void OnSliderChangedGreen(float value)
		{
			animationPreview.color.g = value;
			var img = colorPreview;
			if (img != null) img.color = new Color(animationPreview.color.r, animationPreview.color.g, animationPreview.color.b, animationPreview.color.a);
		}

		public void OnSliderChangedBlue(float value)
		{
			animationPreview.color.b = value;
			var img = colorPreview;
			if (img != null) img.color = new UnityEngine.Color(animationPreview.color.r, animationPreview.color.g, animationPreview.color.b, animationPreview.color.a);
		}

		public void OnSliderChangedTime(float value)
		{
			animationPreview.time = value;
			var t = timePreview;
			if (t != null)
			{
				var v = value.ToString("0.#");
				var label = Localization.GetText("Time");
				t.text = label + ": " + v + "s";
			}
		}

		public void OnValueChangedType(string type)
		{
			var val = (Led.LedAnimation.FadeType)System.Enum.Parse(typeof(Led.LedAnimation.FadeType), type);
			animationPreview.type = val;
			var label = Localization.GetText("Type");
			if (typePreview != null) typePreview.text = label + ": " + type;
		}

		public void OtherLed(int value)
		{
			selected = value;
			var items = itemBlocks;
			if (items != null)
			{
				for (int i = 0; i < items.Length; i++)
				{
					var b = items[i];
					if (b != null) b.interactable = value != i;
				}
			}
			UpdateBlock();
		}

		public void AddAnimation()
		{
			if (leds == null || colorBlocks == null) return;
			int idx = selected;
			if (idx < 0 || idx >= leds.Length) return;
			var led = leds[idx];
			if (led == null) return;
			var anim = led.animations;
			if (anim.points == null) return;
			if (anim.points.Count >= colorBlocks.Length) return;

			var p = new Led.LedAnimation.AnimationPoint
			{
				type = animationPreview.type,
				color = animationPreview.color,
				time = animationPreview.time
			};
			anim.points.Add(p);
			led.ChangedAnimation(anim);

			int bi = anim.points.Count - 1;
			var block = colorBlocks[bi];
			if (block == null) return;
			block.SetActive(true);

			var root = block.transform;
			if (root == null) return;

			var img = root.GetChild(0)?.GetComponent<UnityEngine.UI.Image>();
			if (img != null) img.color = animationPreview.color;

			var txt = root.GetChild(1)?.GetComponent<UnityEngine.UI.Text>();
			if (txt != null)
			{
				txt.text = animationPreview.time.ToString("0.#");
				var c = animationPreview.color;
				var brightness = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
				txt.color = brightness >= 0.5f ? UnityEngine.Color.black : UnityEngine.Color.white;
			}

			var icon = root.GetChild(2)?.GetComponent<UnityEngine.UI.Image>();
			if (icon != null)
			{
				if ((int)animationPreview.type == 1) icon.sprite = sprite_fade;
				else if ((int)animationPreview.type == 0) icon.sprite = sprite_none;
			}
		}

		public void RemoveAnimation(int value)
		{
			var led = leds[selected];
			var animations = led.animations;
			var points = animations.points;
			if (points == null || points.Count < 2) return;
			if (value < 0 || value >= points.Count) return;
			points.RemoveAt(value);
			led.ChangedAnimation(animations);
			UpdateBlock();
		}

		private void UpdateBlock()
		{
			var blocks = colorBlocks;
			if (blocks == null || leds == null || selected < 0 || selected >= leds.Length) return;

			var led = leds[selected];
			var points = led.animations.points;
			if (points == null) return;

			for (int i = 0; i < blocks.Length; i++)
			{
				var go = blocks[i];
				if (go == null) continue;

				if (i < points.Count)
				{
					go.SetActive(true);

					var t = go.transform;
					var p = points[i];

					var img0 = t.GetChild(0).GetComponent<Image>();
					if (img0 != null) img0.color = p.color;

					var txt = t.GetChild(1).GetComponent<Text>();
					if (txt != null)
					{
						txt.text = p.time.ToString("0.#");
						var c = p.color;
						var luma = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
						txt.color = luma >= 0.5f ? Color.black : Color.white;
					}

					var img2 = t.GetChild(2).GetComponent<Image>();
					if (img2 != null) img2.sprite = p.type == Led.LedAnimation.FadeType.Fade ? sprite_fade : sprite_none;
				}
				else
				{
					go.SetActive(false);
				}
			}
		}

		private Led[] GetLed()
		{
			var hardwares = new List<Hardware>();
			var board = system != null ? system.Board : null;
			if (board != null)
			{
				var h3 = board.GetHardwares(HardwareType.RAM);
				if (h3 != null) hardwares.AddRange(h3);
				var h1 = board.GetHardwares(HardwareType.Cooler);
				if (h1 != null) hardwares.AddRange(h1);
				var h6 = board.GetHardwares(HardwareType.Output);
				if (h6 != null) hardwares.AddRange(h6);
			}

			var leds = new List<Led>();
			for (int i = 0; i < hardwares.Count; i++)
			{
				var hw = hardwares[i];
				if (hw == null) continue;
				var led = hw.GetComponent<Led>();
				if (led != null) leds.Add(led);
			}

			return leds.ToArray();
		}
	}
}
