using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PC.Component.Software
{
    public class FileIcon : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IEventSystemHandler
    {
        [SerializeField]
        private Image img;

        [SerializeField]
        private Text nameText;

        private Action<File> callback;
        private RectTransform rectTransform;
        private Canvas parentCanvas;
        private Vector2 dragOffset;
        private bool isDragging = false;

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
            parentCanvas = GetComponentInParent<Canvas>();
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
            if (rectTransform != null)
                rectTransform.anchoredPosition = position;
        }

        public Vector2 GetPosition()
        {
            if (rectTransform != null)
                return rectTransform.anchoredPosition;
            return Vector2.zero;
        }

        public void Open()
        {
            var cb = callback;
            if (cb != null) cb(File);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null || rectTransform == null) return;
            
            // Запоминаем смещение от центра иконки до курсора
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, eventData.position, eventData.pressEventCamera, out dragOffset);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData == null || rectTransform == null) return;
            isDragging = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData == null || rectTransform == null || parentCanvas == null || !isDragging) return;

            Vector2 localPoint;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, eventData.position, eventData.pressEventCamera, out localPoint))
            {
                rectTransform.anchoredPosition += localPoint - dragOffset;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isDragging) return;
            isDragging = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null) return;
            if (eventData.dragging) return;
            if (isDragging) return;
            
            var cb = callback;
            if (cb != null) cb(File);
        }
    }
}
