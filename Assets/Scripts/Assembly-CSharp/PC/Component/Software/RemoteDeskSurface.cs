using UnityEngine;
using UnityEngine.EventSystems;

namespace PC.Component.Software
{
    /// <summary>Pointer bridge from the enlarged preview to its remote UGUI canvas.</summary>
    public class RemoteDeskSurface : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IDragHandler, IScrollHandler
    {
        private RemoteDesk owner;
        private int systemId;
        private RectTransform rect;

        public void Init(RemoteDesk manager, int id)
        {
            owner = manager;
            systemId = id;
            rect = transform as RectTransform;
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (owner != null && systemId != 0) owner.Forward(systemId, data, rect, "down");
        }

        public void OnDrag(PointerEventData data)
        {
            if (owner != null && systemId != 0) owner.Forward(systemId, data, rect, "drag");
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (owner != null && systemId != 0) owner.Forward(systemId, data, rect, "up");
        }

        public void OnScroll(PointerEventData data)
        {
            if (owner != null && systemId != 0) owner.Forward(systemId, data, rect, "scroll");
        }
    }
}
