using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;
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

		/// <summary>
		/// Плата, картинку которой показывает этот монитор.
		/// </summary>
		/// <remarks>
		/// Нужна эффекту артефактов, чтобы узнать, через какие видеокарты идёт
		/// изображение. Только на чтение: подключением по-прежнему заведуют
		/// ConnectBoard и DisconnectBoard.
		/// </remarks>
		public Motherboard ConnectedBoard { get { return currentBoard; } }
        public bool VideoFromGpu { get; set; }
        public GPU VideoGpu { get; set; }
        public void ShowNoSignal(bool visible)
        {
            if (!visible) { StopNoSignalAnimation(); return; }
            // The message is temporary, just as on the ordinary monitor.
            // RefreshAll can fire more than once while plugs settle: do not
            // restart the countdown or keep the banner on indefinitely.
            if (coroutine == null && noSignal != null)
            {
                noSignal.SetActive(false);
                coroutine = StartCoroutine(DisplayNoSignal());
            }
        }

		private RectTransform rect;

		private Vector2 defaultDelta;

		// Used only to migrate old monitor-relative desktop icon coordinates.
		internal Vector2 UnfocusedCanvasSize => defaultDelta;

		private bool isZoom;

		/// <summary>
		/// Игрок сейчас смотрит в этот экран вплотную.
		/// </summary>
		/// <remarks>
		/// Нужно контекстному меню рабочего стола. Пока монитор просто стоит в
		/// комнате, его экран -- часть мира: палец может оказаться над ним,
		/// когда игрок идёт или крутит камеру. Без этой проверки удержание
		/// пальца засчитывалось как долгое нажатие по рабочему столу, и меню
		/// открывалось само. Чем больше экран, тем чаще -- на телевизоре
		/// почти постоянно.
		/// </remarks>
		public bool IsZoomed => isZoom;

		private Coroutine coroutine;

		private Camera cam;

		private int previousCullingMask;

		protected override void Start()
		{
			base.Start();
            PC.Component.Cabling.CablePorts.Install(this);

			canvas = GetComponentInChildren<Canvas>();
			Debug.Assert(canvas != null, "canvas is null");
			rect = canvas.GetComponent<RectTransform>();

			defaultDelta = rect.sizeDelta;

			// Эффект артефактов повреждённой видеокарты. Навешивается из кода,
			// чтобы не добавлять компонент вручную в каждый префаб монитора:
			// их тринадцать, и любой новый монитор получит эффект сам.
			if (GetComponent<GpuArtifacts>() == null) gameObject.AddComponent<GpuArtifacts>();

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
            ApplyBrokenScreen();
		}

        public override void FromData(JObject data)
        {
            base.FromData(data); // restore the flag without replaying Damage() effects
            if (Damaged) ApplyBrokenScreen();
        }

        private void ApplyBrokenScreen()
        {
            // A newly instantiated monitor starts with its prefab material.
            // Hardware.FromData restores Damaged, but does not call Damage(),
            // so restore the visual material explicitly in both save modes.
            if (screen != null && brokenScreenMaterial != null)
                screen.material = brokenScreenMaterial;
        }

		public void RefreshScreen(bool on)
		{
            // Unplugged power means a dark monitor. A powered monitor without
            // video displays No Signal via CablePower.RefreshAll instead.
            StopNoSignalAnimation();
		}

		private void StopNoSignalAnimation()
		{
			if (coroutine != null)
			{
				StopCoroutine(coroutine);
                coroutine = null;
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
            coroutine = null;
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
