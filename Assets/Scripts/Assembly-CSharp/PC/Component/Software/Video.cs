using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

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

		[Header("Назначьте в инспекторе")]
		[Tooltip("Ползунок перемотки видео (диапазон 0..1).")]
		[SerializeField]
		private Slider progressSlider;

		[Tooltip("Текст времени: текущее / всего (например 0:12 / 1:30).")]
		[SerializeField]
		private Text timeText;

		[Tooltip("Кнопка пауза/воспроизведение. Можно повесить на тот же RawImage с видео.")]
		[SerializeField]
		private Button playPauseButton;

		private VideoPlayer player;
		private RenderTexture render;

		// Хост вне окна: при сворачивании окно деактивируется (SetActive(false)),
		// а этот объект живёт — видео продолжает играть.
		private GameObject host;

		private bool hasVideo;
		private bool updatingSlider; // защита от обратной связи код->слайдер

		protected override void Start()
		{
			base.Start();

			render = new RenderTexture(256, 256, 0);

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

			if (progressSlider != null)
			{
				progressSlider.minValue = 0f;
				progressSlider.maxValue = 1f;
				progressSlider.SetValueWithoutNotify(0f);
				progressSlider.onValueChanged.AddListener(OnScrub);
			}
			if (playPauseButton != null)
				playPauseButton.onClick.AddListener(TogglePlayPause);
		}

		private void Update()
		{
			if (player == null || !hasVideo) return;

			double length = player.length;
			double t = player.time;

			if (length > 0.0001 && progressSlider != null)
			{
				updatingSlider = true;
				progressSlider.SetValueWithoutNotify(Mathf.Clamp01((float)(t / length)));
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
			p.Play();
			hasVideo = true;
			if (output != null)
			{
				output.color = Color.white;
				output.texture = render;
			}
			if (warning != null) warning.SetActive(false);
		}

		/// <summary>Пауза/воспроизведение (назначьте на кнопку или на сам RawImage).</summary>
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
				player.time = value * length;
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
			var size = new Vector2(w / scale, h / scale + 40f);
			SetDefaultSize(size);
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
