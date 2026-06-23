using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PC.Component.Software
{
	public class Pong : App, IPointerMoveHandler, IEventSystemHandler
	{
		[SerializeField]
		private float ballSpeed = 1f;

		[SerializeField]
		private float paddleSpeed = 1f;

		[SerializeField]
		private float reactionTime = 0.5f;

		[SerializeField]
		private RectTransform ball;

		[SerializeField]
		private RectTransform paddleLeft;

		[SerializeField]
		private RectTransform paddleRight;

		[SerializeField]
		private Text scoreLeftText;

		[SerializeField]
		private Text scoreRightText;

		[SerializeField]
		private AudioClip hitPaddleSound;

		[SerializeField]
		private AudioClip hitWallSound;

		[SerializeField]
		private AudioClip gameOverSound;

		private int scoreLeft;

		private int scoreRight;

		private Vector2 pos;

		private Vector2 ballDirection;

		private Vector2 paddleLeftPos;

		private Vector2 paddleRightPos;

		private float edge;

		private float nextReaction;

		private float opponentY;

		protected override void Start()
		{
			base.Start();
			ballDirection = Vector2.right;
			var rt = ball;
			if (rt != null) edge = rt.rect.width * 0.5f;
		}

		private void Update()
		{
			var t = UnityEngine.Time.time;
			if (t > nextReaction)
			{
				nextReaction = t + reactionTime;
				opponentY = pos.y;
			}

			if (paddleLeft != null) paddleLeftPos = paddleLeft.anchoredPosition;
			if (paddleRight != null) paddleRightPos = paddleRight.anchoredPosition;

			var step = paddleSpeed * UnityEngine.Time.deltaTime;
			var y = paddleLeftPos.y;
			var targetY = opponentY;
			if (UnityEngine.Mathf.Abs(targetY - y) > step) y += UnityEngine.Mathf.Sign(targetY - y) * step; else y = targetY;

			if (paddleLeft != null) paddleLeftPos.y = ClampPaddlePosition(y, paddleLeft.rect.height * 0.5f);
			if (paddleRight != null) paddleRightPos.y = ClampPaddlePosition(paddleRightPos.y, paddleRight.rect.height * 0.5f);

			if (paddleLeft != null) paddleLeft.anchoredPosition = paddleLeftPos;
			if (paddleRight != null) paddleRight.anchoredPosition = paddleRightPos;

			if (ball == null) return;
			var ballGo = ball.gameObject;
			if (ballGo == null || !ballGo.activeSelf) return;

			pos += ballDirection * ballSpeed * UnityEngine.Time.deltaTime;

			if (rect != null)
			{
				var halfH = rect.rect.height * 0.5f;
				if ((pos.y > halfH - edge && ballDirection.y > 0f) || (pos.y < -halfH + edge && ballDirection.y < 0f))
				{
					ballDirection.y = -ballDirection.y;
					var src = system != null ? system.Board?.Source : null;
					if (src != null) src.PlayOneShot(hitWallSound);
				}
			}

			var r = edge;

			if (ballDirection.x >= 0f)
			{
				if (paddleRight != null)
				{
					var p = paddleRight.anchoredPosition;
					var hw = paddleRight.rect.width * 0.5f;
					var hh = paddleRight.rect.height * 0.5f;
					if (pos.x + r >= p.x - hw && pos.x - r <= p.x + hw && pos.y > p.y - hh && pos.y < p.y + hh)
					{
						var py = Mathf.Clamp((pos.y - p.y) / hh, -1f, 1f);
						ballDirection.x = -Mathf.Abs(ballDirection.x);
						ballDirection.y = py;
						var m = ballDirection.magnitude;
						ballDirection = m > 1e-5f ? ballDirection / m : UnityEngine.Vector2.zero;
						var src = system != null ? system.Board?.Source : null;
						if (src != null) src.PlayOneShot(hitPaddleSound);
					}
					else if (rect != null && pos.x > rect.rect.width * 0.5f - r)
					{
						scoreLeft++;
						if (scoreLeftText != null) scoreLeftText.text = scoreLeft.ToString();
						ballGo.SetActive(false);
						var src = system != null ? system.Board?.Source : null;
						if (src != null) src.PlayOneShot(gameOverSound);
					}
				}
			}
			else
			{
				if (paddleLeft != null)
				{
					var p = paddleLeft.anchoredPosition;
					var hw = paddleLeft.rect.width * 0.5f;
					var hh = paddleLeft.rect.height * 0.5f;
					if (pos.x - r <= p.x + hw && pos.x + r >= p.x - hw && pos.y > p.y - hh && pos.y < p.y + hh)
					{
						var py = Mathf.Clamp((pos.y - p.y) / hh, -1f, 1f);
						ballDirection.x = Mathf.Abs(ballDirection.x);
						ballDirection.y = py;
						var m = ballDirection.magnitude;
						ballDirection = m > 1e-5f ? ballDirection / m : UnityEngine.Vector2.zero;
						var src = system != null ? system.Board?.Source : null;
						if (src != null) src.PlayOneShot(hitPaddleSound);
					}
					else if (rect != null && pos.x < -rect.rect.width * 0.5f + r)
					{
						scoreRight++;
						if (scoreRightText != null) scoreRightText.text = scoreRight.ToString();
						ballGo.SetActive(false);
						var src = system != null ? system.Board?.Source : null;
						if (src != null) src.PlayOneShot(gameOverSound);
					}
				}
			}

			ball.anchoredPosition = pos;
		}

		private float ClampPaddlePosition(float pos, float halfHeight)
		{
			if (rect == null) return pos;
			var h = rect.rect.height;
			var upper = h * 0.5f - halfHeight - edge;
			var lower = halfHeight - h * 0.5f + edge;
			if (pos > upper) pos = upper;
			if (pos < lower) pos = lower;
			return pos;
		}

		private void ResetBall()
		{
			var b = ball;
			if (b == null) return;
			var go = b.gameObject;
			if (go != null) go.SetActive(true);
			pos.x = 0f;
			b.anchoredPosition = new UnityEngine.Vector2(0f, pos.y);
		}

		public void OnPointerMove(PointerEventData eventData)
		{
			var t = paddleRight;
			if (t == null || eventData == null) return;
			var p = t.position;
			p.y = eventData.position.y;
			p.z = 0f;
			t.position = p;
		}
	}
}
