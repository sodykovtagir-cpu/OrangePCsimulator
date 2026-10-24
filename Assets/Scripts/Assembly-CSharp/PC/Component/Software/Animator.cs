using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine.Video;
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
            if (tex == null || framePrefab == null || frameParent == null) return;
			var btn = UnityEngine.Object.Instantiate(framePrefab, frameParent);
            if (btn == null) return;

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

        // Imported clips are loops for the in-game .mov format, not full-length
        // movies. Bound frame count and dimensions to avoid exhausting VRAM or
        // producing enormous saves on a long/high-resolution phone recording.
        private const int ImportFps = 8;
        private const int MaxImportFrames = 96;
        private const int MaxVideoDimension = 512;
        private Coroutine mediaImport;
        private GameObject importHost;
        private VideoPlayer importPlayer;
        private RenderTexture importRender;
        private string importError;
        private List<Texture2D> pendingVideoFrames;
        private bool mediaAvailable = true;

        public void ImportGif()
        {
            if (!mediaAvailable || mediaImport != null) return;
            NativeFilePicker.PickFile(path =>
            {
                if (this == null || !mediaAvailable || system == null ||
                    string.IsNullOrEmpty(path) || mediaImport != null) return;
                try
                {
                    var info = new System.IO.FileInfo(path);
                    if (info.Length > 24000000L)
                    { ImportFailure("GIF is too large (maximum 24 MB)."); return; }
                    Texture2D[] pictures;
                    float[] delays;
                    if (!GifFrames.TryDecode(System.IO.File.ReadAllBytes(path), out pictures, out delays) ||
                        pictures == null || pictures.Length == 0)
                    { ImportFailure("Cannot decode this GIF."); return; }
                    float total = 0f;
                    if (delays != null)
                        foreach (var delay in delays) total += Mathf.Max(.04f, delay);
                    // The editor's .mov has one FPS, not per-frame GIF delays.
                    // Mean delay preserves the overall loop duration.
                    var rate = total > .001f ? pictures.Length / total : ImportFps;
                    ReplaceImportedFrames(pictures, rate);
                }
                catch (Exception ex) { ImportFailure("Cannot import GIF: " + ex.Message); }
            }, NativeFilePicker.ConvertExtensionToFileType("gif"));
        }

        public void ImportVideo()
        {
            if (!mediaAvailable || mediaImport != null) return;
            NativeGallery.GetVideoFromGallery(path =>
            {
                if (this == null || !mediaAvailable || system == null ||
                    string.IsNullOrEmpty(path) || mediaImport != null) return;
                mediaImport = StartCoroutine(ImportVideoFrames(path));
            }, "Import video into Animator", "video/*");
        }

        private void ImportFailure(string message)
        {
            if (system != null) system.ShowMessageBox("Animator", message);
        }

        private void ReplaceImportedFrames(Texture2D[] pictures, float rate)
        {
            if (pictures == null || pictures.Length == 0 || framePrefab == null || frameParent == null)
            { DestroyImported(pictures); ImportFailure("No usable animation frames."); return; }
            int w = pictures[0] != null ? pictures[0].width : 0;
            int h = pictures[0] != null ? pictures[0].height : 0;
            if (w < 1 || h < 1)
            { DestroyImported(pictures); ImportFailure("Invalid frame size."); return; }
            foreach (var picture in pictures)
                if (picture == null || picture.width != w || picture.height != h)
                { DestroyImported(pictures); ImportFailure("Animation frames have different sizes."); return; }
            ClearTimeline();
            frames = new List<Frame>(pictures.Length);
            foreach (var picture in pictures) AddToList(picture);
            if (fpsSlider != null)
            {
                fpsSlider.value = Mathf.Clamp(rate, fpsSlider.minValue, fpsSlider.maxValue);
                OnValueChangedFps(fpsSlider.value);
            }
            else OnValueChangedFps(Mathf.Clamp(rate, 1f, 10f));
            SelectFrame(-1);
            RestartPreview();
        }

        private static void DestroyImported(Texture2D[] textures)
        {
            if (textures == null) return;
            foreach (var texture in textures) if (texture != null) Destroy(texture);
        }

        private void ClearTimeline()
        {
            if (preview != null) preview.texture = null;
            if (canvas != null) canvas.texture = null;
            if (frames != null)
                foreach (var frame in frames)
                    if (frame != null)
                    {
                        if (frame.obj != null) Destroy(frame.obj);
                        if (frame.tex != null) Destroy(frame.tex);
                    }
            frames = new List<Frame>();
            selectedIndex = -1;
            index = 0;
            frameResolution = Vector2Int.zero;
        }

        // Unity VideoPlayer decodes actual media on the player's device. The
        // .mov stored by PCOS is a different, PNG-frame format and is NOT sent
        // to this player. Sample short native clips at eight frames per second.
        private IEnumerator ImportVideoFrames(string path)
        {
            var pictures = new List<Texture2D>();
            pendingVideoFrames = pictures;
            bool committed = false;
            importError = null;
            try
            {
                importHost = new GameObject("Animator video import");
                importHost.transform.SetParent(system != null ? system.transform.root : transform, false);
                importPlayer = importHost.AddComponent<VideoPlayer>();
                var player = importPlayer;
                player.playOnAwake = false;
                player.source = VideoSource.Url;
                player.url = path;
                player.audioOutputMode = VideoAudioOutputMode.None;
                player.renderMode = VideoRenderMode.RenderTexture;
                player.isLooping = false;
                player.skipOnDrop = false;
                player.errorReceived += ImportVideoError;
                try { player.Prepare(); }
                catch (Exception ex) { importError = ex.Message; }
                float deadline = Time.realtimeSinceStartup + 20f;
                while (importError == null && !player.isPrepared && Time.realtimeSinceStartup < deadline)
                    yield return null;
                if (importError == null && player.isPrepared && player.width > 0 && player.height > 0)
                {
                    float scale = Mathf.Min(1f, (float)MaxVideoDimension /
                        Mathf.Max((float)player.width, (float)player.height));
                    int w = Mathf.Max(1, Mathf.RoundToInt(player.width * scale));
                    int h = Mathf.Max(1, Mathf.RoundToInt(player.height * scale));
                    importRender = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32);
                    importRender.Create();
                    player.targetTexture = importRender;
                    player.Play();
                    double nextSample = 0.0;
                    long previousFrame = -1;
                    float started = Time.realtimeSinceStartup;
                    while (importError == null && pictures.Count < MaxImportFrames &&
                           Time.realtimeSinceStartup - started < 30f)
                    {
                        yield return new WaitForEndOfFrame();
                        if (player == null) break;
                        long frame = player.frame;
                        // Some VideoPlayer backends report frame = -1 for URL
                        // sources. In that case use the decoded texture + time.
                        bool fresh = frame >= 0 ? frame != previousFrame : player.texture != null;
                        if (fresh && player.time >= nextSample &&
                            TryReadVideoFrame(importRender, w, h, pictures))
                        {
                            previousFrame = frame;
                            nextSample = (double)pictures.Count / ImportFps;
                        }
                        if (!player.isPlaying && (frame >= 0 ||
                            Time.realtimeSinceStartup - started > 1f)) break;
                        if (player.time >= (double)MaxImportFrames / ImportFps) break;
                    }
                }
                if (pictures.Count > 0 && importError == null)
                {
                    ReplaceImportedFrames(pictures.ToArray(), ImportFps);
                    committed = true;
                    if (pictures.Count == MaxImportFrames)
                        ImportFailure("Imported the first 12 seconds (96 frames). Longer clips are truncated.");
                }
                else ImportFailure(importError ?? "Video could not be decoded on this device.");
            }
            finally
            {
                if (!committed) DestroyImported(pictures.ToArray());
                pendingVideoFrames = null;
                TeardownVideoImport();
                mediaImport = null;
            }
        }

        private static bool TryReadVideoFrame(RenderTexture source, int w, int h,
            List<Texture2D> destination)
        {
            if (source == null || !source.IsCreated()) return false;
            var previous = RenderTexture.active;
            Texture2D texture = null;
            try
            {
                RenderTexture.active = source;
                texture = new Texture2D(w, h, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                texture.Apply();
                destination.Add(texture);
                return true;
            }
            catch (Exception)
            {
                if (texture != null) Destroy(texture);
                return false;
            }
            finally { RenderTexture.active = previous; }
        }

        private void ImportVideoError(VideoPlayer player, string message)
        {
            importError = string.IsNullOrEmpty(message) ? "Video decoder error." : message;
        }

        private void TeardownVideoImport()
        {
            if (importPlayer != null)
            {
                importPlayer.errorReceived -= ImportVideoError;
                importPlayer.Stop();
                importPlayer.targetTexture = null;
                importPlayer = null;
            }
            if (importRender != null)
            {
                importRender.Release();
                Destroy(importRender);
                importRender = null;
            }
            if (importHost != null) { Destroy(importHost); importHost = null; }
        }

        private void CancelVideoImport()
        {
            if (mediaImport != null) { StopCoroutine(mediaImport); mediaImport = null; }
            // Explicitly free captures even on Unity versions that do not
            // dispose a stopped IEnumerator (and therefore skip its finally).
            if (pendingVideoFrames != null)
            {
                DestroyImported(pendingVideoFrames.ToArray());
                pendingVideoFrames = null;
            }
            TeardownVideoImport();
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

        public override void OnSystemStop()
        {
            mediaAvailable = false;
            CancelVideoImport();
            ClearTimeline();
            base.OnSystemStop();
        }

        private void OnDestroy()
        {
            mediaAvailable = false;
            CancelVideoImport();
            ClearTimeline();
        }

		public override void Close()
		{
            mediaAvailable = false;
            CancelVideoImport();
            ClearTimeline();
			base.Close();
		}
	}
}
