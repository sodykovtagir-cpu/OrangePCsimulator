using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PC.Component.Software
{
    public class DesktopIconDragger : MonoBehaviour, IPointerDownHandler, IEventSystemHandler
    {
        private RectTransform rectTransform;
        private RectTransform parentRect;
        private Canvas parentCanvas;
        private Vector2 dragOffset;
        private bool isDragging;

        public static bool IsDragging { get; private set; }

        private Vector2 pointerDownPos;
        private bool hasDragged;
        private bool pointerDownReceived;
        private bool remotePointerActive;
        private RenderMode pointerDownRenderMode;
        private Vector2 pointerDownViewportSize;

        [Header("Grid Settings")]
        [SerializeField] private float cellWidth = 70f;
        [SerializeField] private float cellHeight = 70f;
        [SerializeField] private float spacingX = 20f;
        [SerializeField] private float spacingY = 20f;
        [SerializeField] private float padding = 20f;

        internal DesktopIconGrid Grid => new DesktopIconGrid(cellWidth, cellHeight, spacingX, spacingY, padding);

        private void Awake()
        {
            Init();
        }

        public void Init()
        {
            rectTransform = GetComponent<RectTransform>();
            var parent = transform.parent;
            parentRect = parent != null ? parent.GetComponent<RectTransform>() : null;
            parentCanvas = parent != null ? parent.GetComponentInParent<Canvas>() : null;
        }

        private Camera GetRenderCamera()
        {
            if (parentCanvas == null || parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
            return parentCanvas.worldCamera ?? Camera.main;
        }

        private void Update()
        {
            if (rectTransform == null || parentRect == null)
            {
                Init();
                return;
            }
            if (!pointerDownReceived || remotePointerActive) return;

            // If focus/viewport changes mid-drag, finish at the last desktop position.
            // Do not reinterpret the old pointer using the new monitor's camera/scale.
            if (parentRect.rect.size != pointerDownViewportSize ||
                (parentCanvas != null && parentCanvas.renderMode != pointerDownRenderMode))
            {
                if (isDragging) OnDragEnd();
                else ResetPointerState();
                return;
            }

            if (Input.GetMouseButton(0))
            {
                if (!hasDragged && Vector2.Distance(Input.mousePosition, pointerDownPos) > PointerInput.Slop)
                {
                    hasDragged = true;
                    isDragging = true;
                    IsDragging = true;
                    Vector2 localMousePos;
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        parentRect, Input.mousePosition, GetRenderCamera(), out localMousePos))
                        dragOffset = localMousePos - rectTransform.anchoredPosition;
                }

                if (isDragging)
                {
                    Vector2 localMousePos;
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        parentRect, Input.mousePosition, GetRenderCamera(), out localMousePos))
                        rectTransform.anchoredPosition = localMousePos - dragOffset;
                }
            }
            else
            {
                if (isDragging) OnDragEnd();
                else ResetPointerState();
            }
        }

        // RemoteDesk forwards screen coordinates while the remote canvas is
        // mapped to its render camera. Input.mousePosition is the LOCAL cursor
        // and must never be used to move a remotely controlled desktop icon.
        public void BeginRemoteDrag()
        {
            // OnPointerDown has already been delivered via ExecuteEvents.
            if (pointerDownReceived) remotePointerActive = true;
        }

        public void MoveRemoteDrag(Vector2 position, bool movedEnough)
        {
            if (!remotePointerActive || !pointerDownReceived || parentRect == null) return;
            if (!hasDragged && movedEnough)
            {
                hasDragged = true;
                isDragging = true;
                IsDragging = true;
                Vector2 origin;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect, position, GetRenderCamera(), out origin))
                    dragOffset = origin - rectTransform.anchoredPosition;
            }
            if (!isDragging) return;
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect, position, GetRenderCamera(), out local))
                rectTransform.anchoredPosition = local - dragOffset;
        }

        public void EndRemoteDrag()
        {
            if (!remotePointerActive) return;
            if (isDragging) OnDragEnd();
            else ResetPointerState();
            remotePointerActive = false;
        }

        private void ResetPointerState()
        {
            if (isDragging) IsDragging = false;
            pointerDownReceived = false;
            hasDragged = false;
            isDragging = false;
        }

        private void OnDisable()
        {
            ResetPointerState();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null || !PointerInput.IsPrimary(eventData) || IsDragging) return;
            Init(); // PCOS may have been connected to another physical monitor.
            if (rectTransform == null || parentRect == null) return;

            pointerDownPos = eventData.position;
            pointerDownViewportSize = parentRect.rect.size;
            pointerDownRenderMode = parentCanvas != null ? parentCanvas.renderMode : RenderMode.ScreenSpaceOverlay;
            hasDragged = false;
            isDragging = false;
            pointerDownReceived = true;
        }

        private void OnDragEnd()
        {
            if (rectTransform != null && parentRect != null)
            {
                Vector2 position = SnapToGrid(rectTransform.anchoredPosition, true);
                rectTransform.anchoredPosition = position;
                var os = GetComponentInParent<OS.OperatingSystem>();
                if (os != null) os.SaveIconPosition(GetIconKey(), position);
            }
            ResetPointerState();
        }

        // The Icons parent pivot and FileIcon anchors are both top-left in the prefab.
        // Coordinates and grid spacing are independent of the visible canvas size.
        public Vector2 SnapToGrid(Vector2 position, bool avoidCollisions = false)
        {
            if (parentRect == null) return position;
            return Grid.Snap(position, avoidCollisions ? GetOccupiedCells() : null);
        }

        private HashSet<Vector2Int> GetOccupiedCells()
        {
            var occupied = new HashSet<Vector2Int>();
            var parent = transform.parent;
            if (parent == null) return occupied;
            var grid = Grid;
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child == null || child.gameObject == gameObject || !child.gameObject.activeSelf) continue;
                // Include clipped icons: being outside a monitor must not free their cells.
                if (child.GetComponent<DesktopIconDragger>() == null) continue;
                var rect = child.GetComponent<RectTransform>();
                if (rect != null) grid.ReserveCells(rect.anchoredPosition, occupied);
            }
            // A temporary fullscreen fit may visually vacate another icon's saved
            // cell. Do not let a manual drag create a collision on the physical monitor.
            if (parentCanvas != null && parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                var os = GetComponentInParent<OS.OperatingSystem>();
                if (os != null) os.ReserveCanonicalIconCells(GetIconKey(), occupied);
            }
            return occupied;
        }

        private string GetIconKey()
        {
            var fileIcon = GetComponent<FileIcon>();
            return fileIcon != null && fileIcon.File != null ? fileIcon.File.path : null;
        }
    }
}
