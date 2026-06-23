using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace PC.Component
{
	public abstract class Display : Hardware
	{
		[SerializeField]
		private Renderer screen;

		[SerializeField]
		private Transform screenCanvas;

		[SerializeField]
		private Material brokenScreenMaterial;

		[SerializeField]
		private GameObject trigger;

		[SerializeField]
		private GameObject noSignal;

		protected Canvas canvas;

		private Motherboard currentBoard;

		private RectTransform rect;

		private Vector2 defaultDelta;

		private bool isZoom;

		private Coroutine coroutine;

		private Camera cam;

		private int previousCullingMask;

		protected override void Start()
		{
			base.Start();

			canvas = GetComponentInChildren<Canvas>();
			Debug.Assert(canvas != null, "canvas is null");
			rect = canvas.GetComponent<RectTransform>();

			defaultDelta = rect.sizeDelta;

			PowerChanged += RefreshScreen;

			cam = Camera.main;
			if (canvas != null) canvas.worldCamera = cam;
		}

		public override void Damage()
		{
			base.Damage();
			Switch(false, false);
			StopNoSignalAnimation();
			DisconnectBoard();
			screen.material = brokenScreenMaterial;
		}

		public void RefreshScreen(bool on)
		{
			if (on)
			{
				StopNoSignalAnimation();
				return;
			}

			var routine = DisplayNoSignal();
			coroutine = StartCoroutine(routine);
		}

		private void StopNoSignalAnimation()
		{
			if (coroutine != null)
			{
				StopCoroutine(coroutine);
			}

			if (noSignal != null)
			{
				noSignal.SetActive(false);
			}
		}

		private IEnumerator DisplayNoSignal()
		{
			yield return new WaitForSeconds(2f);
			if (noSignal != null) noSignal.SetActive(true);
			yield return new WaitForSeconds(2f);
			if (noSignal != null) noSignal.SetActive(false);
		}

		public void ApplyScreen(RectTransform display)
		{
			if (display == null) return;
			display.SetParent(screenCanvas);
			display.SetAsLastSibling();
			display.localPosition = Vector3.zero;
			display.localRotation = Quaternion.identity;
			display.localScale = Vector3.one;
			display.sizeDelta = Vector2.zero;
		}

		public void ConnectBoard(Motherboard board)
		{
			if (currentBoard != null) currentBoard.ResetDisplay();
			currentBoard = board;
		}

		public void DisconnectBoard()
		{
			if (currentBoard == null) return;
			AllowZoom(false);
			currentBoard.ResetDisplay();
			currentBoard = null;
		}

		public void AllowZoom(bool value)
        {
			trigger.SetActive(value);
			if (!value) ZoomOut();
        }

		void ZoomIn()
		{
			if (Damaged || isZoom) return;
			isZoom = true;

			var m = Main.Instance;
			if (m) m.Focus(ZoomOut);

			var ad = AdManager.Instance;
			if (ad) ad.HideBanner(true);

			var cv = canvas;
			var tr = cv != null ? cv.transform : null;
			if (tr != null) tr.SetParent(null);
			if (cv != null) cv.renderMode = UnityEngine.RenderMode.ScreenSpaceOverlay;

			var c = cam;
			if (c != null)
			{
				previousCullingMask = c.cullingMask;
				c.cullingMask = 0;
			}
		}

		void ZoomOut()
		{
			if (!isZoom) return;
			isZoom = false;

			var main = Main.Instance;
			if (main != null) main.OnUnfocus();

			var ad = AdManager.Instance;
			if (ad) ad.HideBanner(false);
			ResetCanvas();

			var rt = rect;
			if (rt != null)
			{
				rt.localPosition = UnityEngine.Vector3.zero;
				rt.localRotation = UnityEngine.Quaternion.identity;
				rt.localScale = UnityEngine.Vector3.one;
				rt.sizeDelta = defaultDelta;
			}

			var c = cam;
			if (c != null) c.cullingMask = previousCullingMask;
		}

		protected abstract void ResetCanvas();
	}
}
