using UnityEngine;
using UnityEngine.EventSystems;

namespace PC.Component.Software
{
    public static class PointerInput
    {
        public const float Slop = 28f;
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
            if (Input.touchCount > 0)
                return Input.GetTouch(0).position;
            return Input.mousePosition;
        }

        public static bool PressedThisFrame()
        {
            if (Input.touchCount > 0)
                return Input.GetTouch(0).phase == TouchPhase.Began;
            return Input.GetMouseButtonDown(0);
        }

        public static bool Held()
        {
            if (Input.touchCount > 0)
            {
                var phase = Input.GetTouch(0).phase;
                return phase != TouchPhase.Ended && phase != TouchPhase.Canceled;
            }

            return Input.GetMouseButton(0);
        }
    }
}
