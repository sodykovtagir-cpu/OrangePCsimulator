using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.EventSystems;

namespace PC.Component.Software
{
	public class Video : App
	{
		[SerializeField]
		private RawImage output;

		[SerializeField]
		private Text infoText;

		[SerializeField]
		private GameObject warning;

		[SerializeField]
		private Vector2 maxSize = new Vector2(450f, 600f);

		// Высота полосы управления (прогресс + время) снизу окна.
		private const float ControlBarHeight = 40f;

		private VideoPlayer player;
		private RenderTexture render;

		// Хост, который НЕ является дочерним для окна: при сворачивании окно
		// деактивируется (SetActive(false)), а этот объект остаётся живым — видео
		// продолжает играть.
		private GameObject host;

		private Slider progress;
		private Text timeText;
		private bool updatingSlider; // защищает от обратной связи код->слайдер
		private bool hasVideo;

		protected override void Start()
		{
			base.Start();

			render = new RenderTexture(256, 256, 0);

			// Видео-плеер живёт на отдельном объекте, чтобы не ставиться на паузу
			// при сворачивании окна.
			host = new GameObject("VideoRuntime_" + GetInstanceID());
			host.transform.SetParent(null);
			player = host.AddComponent<VideoPlayer>();
			player.playOnAwake = false;
			player.source = VideoSource.Url;
			player.renderMode = VideoRenderMode.RenderTexture;
			player.targetTexture = render;
			player.audioOutputMode = VideoAudioOutputMode.AudioSource;

			var os = system;
			var board = os != null ? os.Board : null;
			if (board != null && board.Source != null)
				player.SetTargetAudioSource(0, board.Source);

			player.prepareCompleted += PrepareCompleted;
			player.errorReceived += ErrorEventHandler;

			BuildControls();
		}

		private void Update()
		{
			if (player == null || !hasVideo) return;

			double length = player.length;
			double t = player.time;
			if (length > 0.0001)
			{
				updatingSlider = true;
				progress.value = (float)Mathf.Clamp01((float)(t / length));
				updatingSlider = false;
			}
			if (timeText != null)
				timeText.text = FormatTime(t) + " / " + FormatTime(length);
		}

		public void GetVideo()
		{
			var callback = new NativeGallery.MediaPickCallback(PlayVideo);
			NativeGallery.GetVideoFromGallery(callback, "Select a video", "video/*");
		}

		private void PlayVideo(string url)
		{
			if (string.IsNullOrEmpty(url)) return;
			var p = player;
			if (p == null) return;
			p.url = url;
			p.Prepare();
			p.Play();
			hasVideo = true;
			if (output != null)
			{
				output.color = Color.white;
				output.texture = render;
			}
			if (warning != null) warning.SetActive(false);
			if (progress != null) progress.gameObject.SetActive(true);
		}

		/// <summary>Клик по видео — пауза/воспроизведение.</summary>
		public void TogglePlayPause()
		{
			if (player == null || !hasVideo) return;
			if (player.isPlaying) player.Pause();
			else player.Play();
		}

		private void OnScrub(float value)
		{
			if (updatingSlider) return; // значение выставил код, а не пользователь
			if (player == null || !hasVideo) return;
			double length = player.length;
			if (length > 0.0001)
			{
				player.time = value * length;
				if (timeText != null)
					timeText.text = FormatTime(player.time) + " / " + FormatTime(length);
			}
		}

		private void ErrorEventHandler(VideoPlayer source, string message)
		{
			if (warning != null) warning.SetActive(true);
			if (infoText != null) infoText.text = message;
		}

		private void PrepareCompleted(VideoPlayer p)
		{
			if (p == null) return;
			var w = (float)p.width;
			var h = (float)p.height;
			var scale = (maxSize.x / maxSize.y <= w / h) ? w / maxSize.x : h / maxSize.y;
			var size = new Vector2(w / scale, h / scale + ControlBarHeight);
			SetDefaultSize(size);
		}

		// ---- Построение UI управления (прогресс-бар + время + клик-пауза) ----

		private void BuildControls()
		{
			if (output != null)
			{
				// Клик по области видео = пауза/воспроизведение.
				var click = output.gameObject.GetComponent<Button>();
				if (click == null) click = output.gameObject.AddComponent<Button>();
				click.targetGraphic = output;
				click.transition = Selectable.Transition.None;
				click.onClick.RemoveAllListeners();
				click.onClick.AddListener(TogglePlayPause);
			}

			var window = transform as RectTransform;
			if (window == null) return;

			// Полоса управления снизу.
			var barGo = new GameObject("VideoControls", typeof(RectTransform));
			barGo.transform.SetParent(transform, false);
			var bar = barGo.GetComponent<RectTransform>();
			bar.anchorMin = new Vector2(0f, 0f);
			bar.anchorMax = new Vector2(1f, 0f);
			bar.pivot = new Vector2(0.5f, 0f);
			bar.sizeDelta = new Vector2(0f, ControlBarHeight);
			bar.anchoredPosition = new Vector2(0f, 0f);

			// Текст времени (слева).
			var timeGo = new GameObject("Time", typeof(RectTransform), typeof(Text));
			timeGo.transform.SetParent(barGo.transform, false);
			var timeRt = timeGo.GetComponent<RectTransform>();
			timeRt.anchorMin = new Vector2(0f, 0f);
			timeRt.anchorMax = new Vector2(0f, 1f);
			timeRt.pivot = new Vector2(0f, 0.5f);
			timeRt.sizeDelta = new Vector2(96f, 0f);
			timeRt.anchoredPosition = new Vector2(8f, 0f);
			timeText = timeGo.GetComponent<Text>();
			timeText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
			timeText.fontSize = 13;
			timeText.alignment = TextAnchor.MiddleLeft;
			timeText.color = new Color(0.1f, 0.1f, 0.1f, 1f);
			timeText.text = "0:00 / 0:00";
			timeText.horizontalOverflow = HorizontalWrapMode.Overflow;
			timeText.verticalOverflow = VerticalWrapMode.Overflow;
			timeText.raycastTarget = false;

			// Прогресс-бар (справа от текста).
			var sliderGo = new GameObject("Progress", typeof(RectTransform));
			sliderGo.transform.SetParent(barGo.transform, false);
			var sliderRt = sliderGo.GetComponent<RectTransform>();
			sliderRt.anchorMin = new Vector2(0f, 0f);
			sliderRt.anchorMax = new Vector2(1f, 1f);
			sliderRt.pivot = new Vector2(0.5f, 0.5f);
			sliderRt.offsetMin = new Vector2(110f, 6f);
			sliderRt.offsetMax = new Vector2(-10f, -6f);

			progress = BuildSlider(sliderGo.transform);
			progress.minValue = 0f;
			progress.maxValue = 1f;
			progress.value = 0f;
			progress.onValueChanged.AddListener(OnScrub);
		}

		private static Slider BuildSlider(Transform parent)
		{
			var slider = parent.gameObject.AddComponent<Slider>();

			// Background
			var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
			bgGo.transform.SetParent(parent, false);
			var bgRt = bgGo.GetComponent<RectTransform>();
			bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
			bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
			bgGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.18f);

			// Fill Area
			var fillAreaGo = new GameObject("Fill Area", typeof(RectTransform));
			fillAreaGo.transform.SetParent(parent, false);
			var faRt = fillAreaGo.GetComponent<RectTransform>();
			faRt.anchorMin = Vector2.zero; faRt.anchorMax = Vector2.one;
			faRt.offsetMin = new Vector2(8f, 0f); faRt.offsetMax = new Vector2(-8f, 0f);

			var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
			fillGo.transform.SetParent(fillAreaGo.transform, false);
			fillGo.GetComponent<Image>().color = new Color(0.2f, 0.55f, 0.95f, 1f);
			var fillRt = fillGo.GetComponent<RectTransform>();
			fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one;
			fillRt.offsetMin = Vector2.zero; fillRt.offsetMax = Vector2.zero;

			// Handle Slide Area
			var handleAreaGo = new GameObject("Handle Slide Area", typeof(RectTransform));
			handleAreaGo.transform.SetParent(parent, false);
			var haRt = handleAreaGo.GetComponent<RectTransform>();
			haRt.anchorMin = Vector2.zero; haRt.anchorMax = Vector2.one;
			haRt.offsetMin = new Vector2(8f, 0f); haRt.offsetMax = new Vector2(-8f, 0f);

			var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
			handleGo.transform.SetParent(handleAreaGo.transform, false);
			var handleRt = handleGo.GetComponent<RectTransform>();
			handleRt.sizeDelta = new Vector2(16f, 16f);
			handleGo.GetComponent<Image>().color = Color.white;

			slider.targetGraphic = handleGo.GetComponent<Image>();
			slider.fillRect = fillRt;
			slider.handleRect = handleRt;
			slider.direction = Slider.Direction.LeftToRight;
			return slider;
		}

		private static string FormatTime(double seconds)
		{
			if (double.IsNaN(seconds) || seconds < 0) seconds = 0;
			var t = TimeSpan.FromSeconds(seconds);
			int totalMin = (int)t.TotalMinutes;
			return totalMin + ":" + t.Seconds.ToString("00");
		}

		public override void Close()
		{
			var p = player;
			if (p != null) p.Stop();
			if (host != null) Destroy(host);
			base.Close();
		}
	}
}
