using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PC.Component.Software
{
	public class Paint : App
	{
		[SerializeField]
		private InputField canvasWidthInput;

		[SerializeField]
		private InputField canvasHeightInput;

		[SerializeField]
		private GameObject newDocument;

		[SerializeField]
		private GameObject workspace;

		[SerializeField]
		private Button createButton;

		[SerializeField]
		private EventTrigger canvas;

		[SerializeField]
		private RawImage display;

		[SerializeField]
		private RectTransform canvasTransform;

		[SerializeField]
		private Button colorPrefab;

		[SerializeField]
		private Transform colorParent;

		[SerializeField]
		private Color[] allColors;

		[SerializeField]
		private ColorPicker colorPicker;

		[SerializeField]
		private Vector2Int minCanvasSize = new Vector2Int(1, 1);

		[SerializeField]
		private Vector2Int maxCanvasSize = new Vector2Int(70, 70);

		private Vector2Int canvasSize;

		private Texture2D texture;

		private Vector2 lastPoint;

		private bool first;

		private Color drawColor = new Color(0, 0, 0, 1);

		private Image[] colorImages;

		protected override bool ShowMenuBar => false;

		public override void Open(string content)
		{
			base.Open(content);
			if (string.IsNullOrEmpty(content)) return;
			var bytes = System.Convert.FromBase64String(content);
			LoadTexture(bytes);
			var t = texture;
			var w = t.width;
			var h = t.height;
			SetCanvasSize(w, h);
			InitWorkspace();
		}

		public void ApplyPreset(int i)
		{
			var w = canvasWidthInput;
			var h = canvasHeightInput;

			if (i == 0)
			{
				if (w != null) w.text = "32";
				if (h != null) h.text = "32";
				return;
			}

			if (i == 1)
			{
				if (w != null) w.text = "32";
				if (h != null) h.text = "70";
			}
		}

		public void OnValueChangedSize()
		{
			var btn = createButton;
			var wField = canvasWidthInput;
			var hField = canvasHeightInput;
			if (btn == null || wField == null || hField == null) return;

			int w, h;
			bool okW = int.TryParse(wField.text, out w);
			bool okH = int.TryParse(hField.text, out h);

			bool valid = okW && okH
				&& w >= minCanvasSize.x && h >= minCanvasSize.y
				&& w <= maxCanvasSize.x && h <= maxCanvasSize.y;

			btn.interactable = valid;
		}

		public void Create()
		{
			if (canvasWidthInput == null || canvasHeightInput == null) return;

			int w, h;
			if (!int.TryParse(canvasWidthInput.text, out w)) return;
			if (!int.TryParse(canvasHeightInput.text, out h)) return;

			SetCanvasSize(w, h);
			NewCanvas();
			InitWorkspace();
		}

		private void SetCanvasSize(int width, int height)
		{
			canvasSize = new Vector2Int(width, height);
			var rt = canvasTransform;
			if (rt != null)
			{
				var arf = rt.GetComponent<AspectRatioFitter>();
				if (arf != null) arf.aspectRatio = (float)width / (float)height;
			}
		}

		private void InitWorkspace()
		{
			if (newDocument != null) newDocument.SetActive(false);
			if (workspace != null) workspace.SetActive(true);

			var et = canvas;
			if (et != null)
			{
				var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
				down.callback.AddListener(_ => PointerDown(_));
				var drag = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
				drag.callback.AddListener(_ => Drag(_));
				et.triggers.Add(down);
				et.triggers.Add(drag);
			}

			if (allColors != null)
			{
				colorImages = new Image[allColors.Length];
				for (int i = 0; i < allColors.Length; i++)
				{
					var btn = Instantiate(colorPrefab, colorParent);
					var img = btn != null ? btn.GetComponent<Image>() : null;
					if (img != null) colorImages[i] = img;
					int idx = i;
					if (btn != null) btn.onClick.AddListener(() => SelectColor(idx));
				}
				RefreshColors();
			}

			var os = system;
			if (os != null) os.ShowMenuBar(this);
		}

		private void NewCanvas()
		{
			int w = canvasSize.x;
			int h = canvasSize.y;

			var tex = new UnityEngine.Texture2D(w, h, UnityEngine.TextureFormat.RGBA32, false);
			var pixels = new UnityEngine.Color[w * h];
			var white = UnityEngine.Color.white;
			for (int i = 0; i < pixels.Length; i++) pixels[i] = white;
			tex.SetPixels(pixels);
			tex.Apply();

			SetTexture(tex);
		}

		private void RefreshColors()
		{
			var colors = allColors;
			var imgs = colorImages;
			if (colors == null || imgs == null) return;
			int n = System.Math.Min(colors.Length, imgs.Length);
			for (int i = 0; i < n; i++)
			{
				var img = imgs[i];
				if (img != null) img.color = colors[i];
			}
		}

		private void PointerDown(BaseEventData eventData)
		{
			first = true;
			var ped = eventData as PointerEventData;
			if (ped != null) Draw(ped.position);
		}

		private void Drag(BaseEventData eventData)
		{
			var ped = eventData as PointerEventData;
			if (ped == null) return;
			Draw(ped.position);
		}

		private void Draw(Vector2 position)
		{
			if (canvasTransform == null || texture == null) return;

			Vector2 local;
			RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasTransform, position, null, out local);

			var rect = canvasTransform.rect;
			var size = rect.size;
			var pos = local + size * 0.5f;

			float px = pos.x / size.x * canvasSize.x;
			float py = pos.y / size.y * canvasSize.y;

			if (first)
			{
				lastPoint = new Vector2(px, py);
				texture.SetPixel(Mathf.RoundToInt(px), Mathf.RoundToInt(py), drawColor);
				first = false;
			}
			else
			{
				var current = new Vector2(px, py);
				DrawLine(texture, lastPoint, current, drawColor);
				lastPoint = current;
			}

			texture.Apply();
		}

		private void DrawLine(Texture2D tex, Vector2 p1, Vector2 p2, Color col)
		{
			if (tex == null) return;

			float dx = p2.x - p1.x;
			float dy = p2.y - p1.y;
			int steps = Mathf.Max(1, Mathf.CeilToInt(
				Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy))
			));

			for (int i = 0; i <= steps; i++)
			{
				float t = i / (float)steps;
				int x = (int)(p1.x + dx * t);
				int y = (int)(p1.y + dy * t);
				tex.SetPixel(x, y, col);
			}
		}

		private void SelectColor(int index)
		{
			if (allColors == null || index < 0 || index >= allColors.Length) return;
			drawColor = allColors[index];
		}

		public void PickColor()
		{
			var current = drawColor;
			var picker = colorPicker;
			if (picker == null) return;

			picker.PickColor(current, c =>
			{
				drawColor = c;
				var colors = allColors;
				if (colors == null || colors.Length == 0) return;

				for (int i = colors.Length - 1; i >= 1; i--) colors[i] = colors[i - 1];
				colors[0] = c;
				RefreshColors();
			});
		}

		public void Save()
		{
			var png = ImageConversion.EncodeToPNG(texture);
			var content = System.Convert.ToBase64String(png);
			var os = system;
			if (os == null) return;
			var dlg = os.SaveDialog;
			var exts = new string[1];
			exts[0] = FileName;
			if (dlg != null) dlg.ShowDialog("Untitled", content, exts);
		}

		public void OpenLocalFile()
		{
			NativeGallery.GetImageFromGallery(path =>
			{
				if (string.IsNullOrEmpty(path)) return;
				var bytes = System.IO.File.ReadAllBytes(path);
				LoadTexture(bytes);
				var tex = texture;
				if (tex == null) return;
				int targetX = canvasSize.x;
				int targetY = canvasSize.y;
				if (tex.width != targetX || tex.height != targetY) Resize(tex, targetX, targetY);
			}, "Select an image", "image/*");
		}

		public void PrintPicture()
		{
			var os = system;
			if (os != null) os.PrintPicture(texture);
		}

		private void LoadTexture(byte[] bytes)
		{
			var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
			ImageConversion.LoadImage(tex, bytes);
			SetTexture(tex);
		}

		private void SetTexture(Texture2D tex)
		{
			var old = texture;
			if (old != null) Destroy(old);
			if (tex == null) return;
			tex.filterMode = FilterMode.Point;
			tex.wrapMode = TextureWrapMode.Clamp;
			texture = tex;
			var img = display;
			if (img != null) img.texture = tex;
		}

		private void Resize(Texture2D texture2D, int targetX, int targetY)
		{
			var rt = new RenderTexture(targetX, targetY, 24);
			RenderTexture.active = rt;
			Graphics.Blit(texture2D, rt);
			if (texture2D == null) return;
			texture2D.Reinitialize(targetX, targetY);
			var source = new Rect(0f, 0f, targetX, targetY);
			texture2D.ReadPixels(source, 0, 0);
			texture2D.Apply();
		}

		public override void Close()
		{
			var tex = texture;
			if (tex != null) Destroy(tex);
			base.Close();
		}
	}
}
