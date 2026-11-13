using UnityEngine;
using UnityEngine.EventSystems;

namespace PC.Component.Software.Sites
{
    public sealed class SiteElementHandle : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        SiteCanvas owner;
        RectTransform canvas, rect;
        string id;
        bool text, held, dragging;
        float pressedAt;
        Vector2 pointerStart, positionStart;
        public void Bind(SiteCanvas view, string elementId, RectTransform design, bool isText)
        { owner = view; id = elementId; canvas = design; text = isText; rect = (RectTransform)transform; }
        public void OnPointerDown(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            held = true; dragging = false; pressedAt = Time.unscaledTime; owner.Select(id);
        }
        public void OnPointerUp(PointerEventData e) { held = false; }
        public void OnBeginDrag(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            dragging = RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, e.position, e.pressEventCamera, out pointerStart);
            positionStart = rect.anchoredPosition; held = false;
        }
        public void OnDrag(PointerEventData e)
        {
            Vector2 point;
            if (dragging && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, e.position, e.pressEventCamera, out point))
                rect.anchoredPosition = positionStart + point - pointerStart;
        }
        public void OnEndDrag(PointerEventData e)
        { if (dragging) { dragging = false; owner.Move(id, rect.anchoredPosition); } }
        void Update()
        {
            if (held && text && !dragging && Time.unscaledTime - pressedAt >= .55f)
            { held = false; owner.EditText(id); }
        }
    }
}
