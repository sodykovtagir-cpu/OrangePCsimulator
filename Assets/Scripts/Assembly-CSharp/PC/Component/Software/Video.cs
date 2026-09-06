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

		[Tooltip("Значок паузы поверх видео: виден, когда видео на паузе, и скрыт при воспроизведении.")]
		[SerializeField]
		private GameObject pauseIndicator;

		private VideoPlayer player;
		private RenderTexture render;

		// Хост вне окна: при сворачивании окно деактивируется (SetActive(false)),
		// а этот объект живёт — видео продолжает играть. Но он НЕ должен висеть в
		// корне сцены: при поломке/выключении компа система (System) уничтожается,
		// а объект, припарентенный к null, пережил бы это и видео/звук играли бы
		// на выключенном ПК. Поэтому вешаем его под корень системы (компьютера) —
		// сворачивание окна его не трогает (деактивируется только окно), а при
		// уничтожении компа он удаляется вместе с ним. Доп. подстраховка — OnDestroy.
		private GameObject host;

		private bool hasVideo;
		private bool updatingSlider; // защита от обратной связи код->слайдер

		protected override void Start()
		{
			base.Start();

			render = new RenderTexture(256, 256, 0);

			host = new GameObject("VideoRuntime_" + GetInstanceID());
			// Привязываем к корню системы (компьютера), а НЕ к null: хост переживёт
			// сворачивание окна, но будет уничтожен вместе с ПК при выключении/поломке.
			var sys = system != null ? system.transform : null;
			var parentRoot = sys != null ? sys.root : null;
			if (parentRoot != null) host.transform.SetParent(parentRoot, false);
			else host.transform.SetParent(null);
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
			if (pauseIndicator != null)
				pauseIndicator.SetActive(false);
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

			// Значок паузы виден, когда видео поставлено на паузу.
			if (pauseIndicator != null)
			{
				bool paused = !player.isPlaying;
				if (pauseIndicator.activeSelf != paused)
					pauseIndicator.SetActive(paused);
			}
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
			if (pauseIndicator != null) pauseIndicator.SetActive(false);
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
			TeardownHost();
			base.Close();
		}

		// Система выключается/ломается — немедленно глушим воспроизведение
		// (видео не должно играть на выключенном/сломанном ПК). Окно не закрываем:
		// при выключении объект и так уничтожится вместе с системой.
		public override void OnSystemStop()
		{
			base.OnSystemStop();
			var p = player;
			if (p != null)
			{
				try { p.Pause(); } catch { }
			}
		}

		// Гарантированно гасим видео/звук и освобождаем ресурсы. Вызывается и при
		// закрытии окна, и при уничтожении приложения (выключение/поломка ПК),
		// чтобы VideoPlayer/RenderTexture не «висели» и не играли после гибели ПК.
		private bool tornDown;

		private void TeardownHost()
		{
			if (tornDown) return;
			tornDown = true;

			try
			{
				var p = player;
				if (p != null) p.Stop();
			}
			catch { /* плеер мог быть уже уничтожен вместе с хостом */ }

			try
			{
				if (render != null)
				{
					render.Release();
					if (Application.isPlaying) Destroy(render);
				}
			}
			catch { }

			if (host != null)
			{
				if (Application.isPlaying) Destroy(host);
				else DestroyImmediate(host);
			}
		}

		private void OnDestroy()
		{
			TeardownHost();
		}
	}
}
