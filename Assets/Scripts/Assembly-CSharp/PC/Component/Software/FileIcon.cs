using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PC.Component.Software
{
    public class FileIcon : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler, IEventSystemHandler
    {
        [SerializeField]
        private Image img;

        [SerializeField]
        private Text nameText;

        private Action<File> callback;
        private RectTransform rectTransform;
        private float pointerDownTime;
        private Vector2 pointerDownPos;
        private bool isPointerDown;
        private int activePointerId;
        private bool openedMenu;

        private const float longPressDuration = PointerInput.LongPress;
        private const float pointerMoveThreshold = PointerInput.Slop;

        public File File { get; private set; }

        public Sprite Sprite
        {
            set
            {
                if (img != null) img.sprite = value;
            }
        }

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
        }

        public void Init(File file, Action<File> callback)
        {
            this.callback = callback;
            this.File = file;
            if (file == null) return;
            if (file.hidden)
            {
                var go = gameObject;
                if (go != null) go.SetActive(false);
            }
            var t = nameText;
            if (t != null) t.text = File.NameWithoutExtension(file.path);
        }

        public void SetPosition(Vector2 position)
        {
            // PCOS populates desktop icons during Boot, before its Desktop
            // hierarchy is shown. Awake is not guaranteed to have cached the
            // RectTransform yet; dropping this first position leaves every
            // icon stacked at the prefab's (0, 0) by the screen edge.
            if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
            if (rectTransform != null) rectTransform.anchoredPosition = position;
        }

        public Vector2 GetPosition()
        {
            if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
            return rectTransform != null ? rectTransform.anchoredPosition : Vector2.zero;
        }

        public void Open()
        {
            var cb = callback;
            if (cb != null) cb(File);
        }

        private void Update()
        {
            if (!isPointerDown || openedMenu) return;
            if (DesktopIconDragger.IsDragging)
            {
                isPointerDown = false;
                return;
            }

            Vector2 current;
            if (!PointerInput.TryGetHeldPosition(activePointerId, out current) ||
                Vector2.Distance(current, pointerDownPos) >= pointerMoveThreshold)
            {
                isPointerDown = false;
                return;
            }
            if (Time.unscaledTime - pointerDownTime > longPressDuration)
            {
                openedMenu = true;
                isPointerDown = false;
                PointerInput.ConsumedClick = true;
                var menu = DesktopContextMenu.For(this);
                if (menu != null && File != null && menu.CanReceiveDesktopPointer(current))
                    menu.ShowFileMenu(File, current);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!PointerInput.IsPrimary(eventData))
                return;

            var menu = DesktopContextMenu.For(this);
            if (menu != null && !menu.CanReceiveDesktopPointer(eventData.position)) return;
            activePointerId = eventData.pointerId;
            PointerInput.ConsumedClick = false;
            pointerDownTime = Time.unscaledTime;
            pointerDownPos = eventData.position;
            isPointerDown = true;
            openedMenu = false;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData == null || eventData.pointerId == activePointerId) isPointerDown = false;
        }

        private void OnDisable()
        {
            isPointerDown = false;
            openedMenu = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null) return;
            if (eventData.dragging) return;
            if (DesktopIconDragger.IsDragging) return;
            if (openedMenu || PointerInput.ConsumedClick) return;
            var menu = DesktopContextMenu.For(this);
            if (menu != null && !menu.CanReceiveDesktopPointer(eventData.position)) return;

            if (eventData.button == PointerEventData.InputButton.Right)
            {
                if (menu != null && File != null)
                    menu.ShowFileMenu(File, eventData.position);
                return;
            }

            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            var cb = callback;
            if (cb != null) cb(File);
        }
    }
}
