using UnityEngine;
using UnityEngine.EventSystems;

namespace PC.Component.Software
{
    public static class PointerInput
    {
        public const float Slop = 24f;
        public const float LongPress = 0.45f;

        public static bool ConsumedClick;

        public static bool IsPrimary(PointerEventData eventData)
        {
            if (eventData == null) return false;
            if (eventData.button == PointerEventData.InputButton.Right) return false;
            if (eventData.button == PointerEventData.InputButton.Middle) return false;
            return true;
        }

        public static Vector2 ScreenPosition()
        {
            if (Input.GetMouseButton(0) || Input.GetMouseButtonDown(0) || Input.GetMouseButtonUp(0))
                return Input.mousePosition;
            if (Input.touchCount > 0)
                return Input.GetTouch(0).position;
            return Input.mousePosition;
        }
    }
}
