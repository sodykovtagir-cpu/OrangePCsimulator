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

		private VideoPlayer player;

		private RenderTexture render;

		protected override void Start()
		{
			base.Start();

			player = GetComponent<VideoPlayer>();

			var rt = new RenderTexture(256, 256, 0);
			render = rt;
			if (player != null) player.targetTexture = rt;

			var os = system;
			var board = os != null ? os.Board : null;
			if (player != null && board != null)
			{
				player.SetTargetAudioSource(0, board.Source);
				player.prepareCompleted += PrepareCompleted;
				player.errorReceived += ErrorEventHandler;
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
			if (output != null)
			{
				output.color = Color.white;
				output.texture = render;
			}
			if (warning != null) warning.SetActive(false);
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

		public override void Close()
		{
			var p = player;
			if (p == null) return;
			p.Stop();
			base.Close();
		}
	}
}
