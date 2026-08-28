using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PC.Component.Software
{
    /// <summary>
    /// Свободное перемещение иконок по рабочему столу с привязкой к сетке.
    /// Ограничивает перемещение границами экрана.
    /// Возвращает иконку если она за экраном.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class DesktopIconDragger : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Настройки сетки")]
        [SerializeField] private float gridSize = 10f; // Размер ячейки сетки
        [SerializeField] private bool snapToGrid = true; // Привязка к сетке

        [Header("Ограничения")]
        [SerializeField] private float margin = 10f; // Отступ от краёв экрана

        private RectTransform rectTransform;
        private Canvas canvas;
        private Vector2 originalPosition;
        private bool isDragging = false;

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            canvas = GetComponentInParent<Canvas>();
        }

        void Start()
        {
            // Проверяем позицию при старте
            ClampToScreen();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            isDragging = true;
            originalPosition = rectTransform.anchoredPosition;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging) return;

            // Двигаем иконку за курсором
            Vector2 newPosition;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.GetComponent<RectTransform>(),
                eventData.position,
                canvas.worldCamera,
                out newPosition))
            {
                rectTransform.anchoredPosition = newPosition;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            isDragging = false;

            // Привязка к сетке
            if (snapToGrid && gridSize > 0)
            {
                Vector2 pos = rectTransform.anchoredPosition;
                pos.x = Mathf.Round(pos.x / gridSize) * gridSize;
                pos.y = Mathf.Round(pos.y / gridSize) * gridSize;
                rectTransform.anchoredPosition = pos;
            }

            // Ограничение границами экрана
            ClampToScreen();

            // Проверяем что иконка не за экраном
            if (!IsOnScreen())
            {
                // Возвращаем на ближайшую позицию на экране
                ReturnToNearestScreenPosition();
            }
        }

        /// <summary>
        /// Ограничивает позицию границами экрана
        /// </summary>
        private void ClampToScreen()
        {
            if (canvas == null) return;

            var canvasRect = canvas.GetComponent<RectTransform>().rect;
            Vector2 pos = rectTransform.anchoredPosition;

            float halfWidth = canvasRect.width / 2 - margin;
            float halfHeight = canvasRect.height / 2 - margin;

            pos.x = Mathf.Clamp(pos.x, -halfWidth, halfWidth);
            pos.y = Mathf.Clamp(pos.y, -halfHeight, halfHeight);

            rectTransform.anchoredPosition = pos;
        }

        /// <summary>
        /// Проверяет находится ли иконка на экране
        /// </summary>
        private bool IsOnScreen()
        {
            if (canvas == null) return true;

            var canvasRect = canvas.GetComponent<RectTransform>().rect;
            Vector2 pos = rectTransform.anchoredPosition;
            Vector2 size = rectTransform.sizeDelta;

            float halfWidth = canvasRect.width / 2;
            float halfHeight = canvasRect.height / 2;

            // Проверяем что хотя бы часть иконки видна
            bool visibleX = (pos.x + size.x / 2) > -halfWidth && (pos.x - size.x / 2) < halfWidth;
            bool visibleY = (pos.y + size.y / 2) > -halfHeight && (pos.y - size.y / 2) < halfHeight;

            return visibleX && visibleY;
        }

        /// <summary>
        /// Возвращает иконку на ближайшую позицию на экране
        /// </summary>
        private void ReturnToNearestScreenPosition()
        {
            if (canvas == null) return;

            var canvasRect = canvas.GetComponent<RectTransform>().rect;
            Vector2 pos = rectTransform.anchoredPosition;
            Vector2 size = rectTransform.sizeDelta;

            float halfWidth = canvasRect.width / 2 - margin;
            float halfHeight = canvasRect.height / 2 - margin;

            // Находим ближайшую точку на экране
            Vector2 nearestPos = new Vector2(
                Mathf.Clamp(pos.x, -halfWidth, halfWidth),
                Mathf.Clamp(pos.y, -halfHeight, halfHeight)
            );

            // Привязка к сетке
            if (snapToGrid && gridSize > 0)
            {
                nearestPos.x = Mathf.Round(nearestPos.x / gridSize) * gridSize;
                nearestPos.y = Mathf.Round(nearestPos.y / gridSize) * gridSize;
            }

            rectTransform.anchoredPosition = nearestPos;
        }

        /// <summary>
        /// Принудительно проверить позицию (вызвать после изменения размера окна)
        /// </summary>
        public void ForceClamp()
        {
            ClampToScreen();
            if (!IsOnScreen())
            {
                ReturnToNearestScreenPosition();
            }
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (gridSize < 0) gridSize = 0;
            if (margin < 0) margin = 0;
        }
#endif
    }
}
