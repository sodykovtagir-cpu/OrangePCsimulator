using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
	public class Animator : App
	{
		private class Frame
		{
			public GameObject obj;

			public Image outline;

			public Texture2D tex;

			public Frame(GameObject obj, Image outline, Texture2D tex)
            {
				this.obj = obj;
				this.outline = outline;
				this.tex = tex;
            }
		}

		[SerializeField]
		private RawImage canvas;

		[SerializeField]
		private RawImage preview;

		[SerializeField]
		private Button framePrefab;

		[SerializeField]
		private Transform frameParent;

		[SerializeField]
		private Color selectedColor;

		[SerializeField]
		private Text fpsText;

		[SerializeField]
		private Slider fpsSlider;

		[SerializeField]
		private Button moveUpButton;

		[SerializeField]
		private Button moveDownButton;

		[SerializeField]
		private Button removeButton;

		private Color frameColor;

		private float fps;

		private float lastTime;

		private int index;

		private int selectedIndex;

		private List<Frame> frames;

		private Vector2Int frameResolution;

		private void Awake()
		{
			var prefab = framePrefab;
			if (prefab == null) return;
			var t = prefab.transform;
			if (t == null || t.childCount == 0) return;
			var img = t.GetChild(0).GetComponent<Image>();
			if (img == null) return;
			frameColor = img.color;
		}

		private void Update()
		{
			if (frames == null) return;
			if (frames.Count > 0 && Time.time - lastTime > 1f / fps)
			{
				StepPreview();
				lastTime = Time.time;
			}
		}

		private void StepPreview()
		{
			var list = frames;
			index++;
			if (list == null || list.Count == 0) return;
			index %= list.Count;
			var img = preview;
			var frame = list[index];
			if (img != null && frame != null) img.texture = frame.tex;
		}

		private void RestartPreview()
		{
			lastTime = Time.time;
			index = 0;
			var img = preview;
			if (img == null) return;
			Texture tex = null;
			if (frames != null && frames.Count > 0)
			{
				var frame = frames[0];
				if (frame != null) tex = frame.tex;
			}
			img.texture = tex;
		}

		public override void Open(string content)
		{
			base.Open(content);

			if (string.IsNullOrEmpty(content))
			{
				frames = new List<Frame>();
			}
			else
			{
				var mov = FormatConverter.StringToMov(content, false);
				if (mov != null && mov.texs != null)
				{
					frames = new List<Frame>(mov.texs.Length);
					for (int i = 0; i < mov.texs.Length; i++)
					{
						var tex = mov.texs[i];
						AddToList(tex);
					}
					if (fpsSlider != null) fpsSlider.SetValueWithoutNotify(1f / mov.interval);
				}
			}

			if (fpsSlider != null) OnValueChangedFps(fpsSlider.value);
		}

		private void AddToList(UnityEngine.Texture2D tex)
		{
			var btn = UnityEngine.Object.Instantiate(framePrefab, frameParent);
			if (btn == null || tex == null) return;

			var raw = btn.GetComponent<UnityEngine.UI.RawImage>();
			if (raw != null) raw.texture = tex;

			int w = tex.width;
			int h = tex.height;

			var child = btn.transform != null && btn.transform.childCount > 0 ? btn.transform.GetChild(0) : null;
			var border = child != null ? child.GetComponent<UnityEngine.UI.Image>() : null;

			var go = btn.gameObject;
			var f = new Frame(go, border, tex);

			if (frames != null)
			{
				if (frames.Count == 0)
				{
					frameResolution = new Vector2Int(w, h);
					var ar = preview != null ? preview.GetComponent<UnityEngine.UI.AspectRatioFitter>() : null;
					if (ar != null) ar.aspectRatio = (float)w / h;
					var ar2 = canvas != null ? canvas.GetComponent<UnityEngine.UI.AspectRatioFitter>() : null;
					if (ar2 != null) ar2.aspectRatio = (float)w / h;
				}
				frames.Add(f);
			}

			var onClick = btn.onClick;
			if (onClick != null) onClick.AddListener(() =>
			{
				if (preview != null) preview.texture = tex;
			});
		}

		public void SelectFrame(int index)
		{
			selectedIndex = index;

			if (frames != null)
			{
				for (int i = 0; i < frames.Count; i++)
				{
					var f = frames[i];
					if (f != null && f.outline != null) f.outline.color = frameColor;
				}
			}

			if (selectedIndex == -1)
			{
				if (canvas != null) canvas.texture = null;
			}
			else if (frames != null && index >= 0 && index < frames.Count)
			{
				var f = frames[index];
				if (f != null)
				{
					if (f.outline != null) f.outline.color = selectedColor;
					if (canvas != null) canvas.texture = f.tex;
				}
			}

			if (removeButton != null) removeButton.interactable = selectedIndex != -1;

			int count = frames != null ? frames.Count : 0;
			bool canUp = count >= 2 && selectedIndex > 0;
			bool canDown = count >= 2 && selectedIndex >= 0 && selectedIndex < count - 1;

			if (moveUpButton != null) moveUpButton.interactable = canUp;
			if (moveDownButton != null) moveDownButton.interactable = canDown;
		}

		public void Save()
		{
			var os = system;
			var dlg = os != null ? os.SaveDialog : null;
			if (dlg == null) return;

			Texture2D[] texs;
			if (frames != null && frames.Count > 0)
			{
				texs = new Texture2D[frames.Count];
				for (int i = 0; i < frames.Count; i++) texs[i] = frames[i] != null ? frames[i].tex : null;
			}
			else
			{
				texs = new Texture2D[0];
			}

			var mov = new FormatConverter.Mov(texs, 1f / fps);
			var content = FormatConverter.MovToString(mov);

			var extensions = new string[1];
			extensions[0] = FileName;

			dlg.ShowDialog("Untitled", content, extensions);
		}

		public void Upload()
		{
			var os = system;
			var picker = os != null ? os.DevicePicker : null;
			if (picker == null) return;

			picker.PickDevice(os, device =>
			{
				var display = device as LedDisplay;
				if (display == null) return;

				if (display.targetResolution.x == frameResolution.x && display.targetResolution.y == frameResolution.y)
				{
					StartCoroutine(UploadToDisplay(display));
				}
				else
				{
					var title = Localization.GetText("Error");
					var msg = Localization.GetText("Only supports {0}x{1} resolution");
					msg = string.Format(msg, display.targetResolution.x, display.targetResolution.y);
					var s = system;
					if (s != null) s.ShowMessageBox(title, msg);
				}
			}, 2);
		}

		private IEnumerator UploadToDisplay(LedDisplay display)
		{
			var os = system;
			var pb = os != null ? os.ProgressBar : null;

			if (pb != null)
			{
				var name = Localization.GetText("Uploading");
				var barColor = new Color(0.3f, 1f, 0.3f, 1f);
				pb.CallProgressBar(name, barColor, null);

				float t = 0f;
				while (t < 1f)
				{
					t += Time.deltaTime;
					pb.SetProgress(t);
					yield return null;
				}

				pb.CloseProgressBar();
			}

			if (display == null) yield break;

			var list = new List<Texture2D>();
			if (frames != null)
			{
				for (int i = 0; i < frames.Count; i++)
				{
					var f = frames[i];
					list.Add(f != null ? f.tex : null);
				}
			}

			display.UploadPattern(list, 1f / fps);
		}

		public void OnValueChangedFps(float value)
		{
			fps = value;
			if (fpsText != null) fpsText.text = value.ToString("0") + " FPS";
		}

		public void AddFrame()
		{
			var os = system;
			if (os == null) return;

			os.SelectFile(".pic", file =>
			{
				if (file == null) return;

				var tex = FormatConverter.StringToTexture(file.content);
				var list = frames;
				if (list == null) return;

				if (list.Count < 1)
				{
					AddToList(tex);
					RestartPreview();
					return;
				}

				if (tex != null && tex.width == frameResolution.x && tex.height == frameResolution.y)
				{
					AddToList(tex);
					RestartPreview();
					return;
				}

				var title = Localization.GetText("Error");
				var msg = Localization.GetText("Only supports {0}x{1} resolution");
				msg = string.Format(msg, frameResolution.x, frameResolution.y);
				var s = system;
				if (s != null) s.ShowMessageBox(title, msg);
			});
		}

		public void MoveUp()
		{
			var list = frames;
			int i = selectedIndex;
			if (list == null || i <= 0 || i >= list.Count) return;

			var f = list[i];
			if (f != null && f.obj != null) f.obj.transform.SetSiblingIndex(i - 1);

			var prev = list[i - 1];
			list[i - 1] = f;
			list[i] = prev;

			SelectFrame(i - 1);
			RestartPreview();
		}

		public void MoveDown()
		{
			var list = frames;
			int i = selectedIndex;
			if (list == null || i < 0 || i >= list.Count - 1) return;

			var f = list[i];
			if (f != null && f.obj != null) f.obj.transform.SetSiblingIndex(i + 1);

			var next = list[i + 1];
			list[i + 1] = f;
			list[i] = next;

			SelectFrame(i + 1);
			RestartPreview();
		}

		public void RemoveFrame()
		{
			var list = frames;
			int i = selectedIndex;
			if (list == null || i < 0 || i >= list.Count) return;

			var f = list[i];
			if (f != null)
			{
				if (f.tex != null) Destroy(f.tex);
				if (f.obj != null) Destroy(f.obj);
			}

			list.RemoveAt(i);

			int newIndex = i;
			int last = list.Count - 1;
			if (newIndex > last) newIndex = last;

			SelectFrame(newIndex);
			RestartPreview();
		}

		public override void Close()
		{
			var list = frames;
			if (list != null)
			{
				for (int i = 0; i < list.Count; i++)
				{
					var f = list[i];
					if (f != null && f.tex != null) Destroy(f.tex);
				}
			}
			base.Close();
		}
	}
}
