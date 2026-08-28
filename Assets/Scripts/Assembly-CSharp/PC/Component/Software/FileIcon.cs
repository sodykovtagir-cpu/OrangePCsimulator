using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PC.Component.Software
{
    public class FileIcon : MonoBehaviour, IPointerClickHandler, IEventSystemHandler
    {
        [SerializeField]
        private Image img;

        [SerializeField]
        private Text nameText;

        private Action<File> callback;
        private RectTransform rectTransform;

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

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null) return;
            if (eventData.dragging) return;
            if (DesktopIconDragger.WasDragged) return;
            var cb = callback;
            if (cb != null) cb(File);
        }
    }
}
