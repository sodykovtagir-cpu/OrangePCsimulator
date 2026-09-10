using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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

        // Track the finger that actually started the gesture, not touch 0 or a
        // stale mouse position. Released/cancelled touches cannot become holds.
        public static bool TryGetHeldPosition(int pointerId, out Vector2 position)
        {
            position = Vector2.zero;
            if (pointerId >= 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var touch = Input.GetTouch(i);
                    if (touch.fingerId != pointerId) continue;
                    if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) return false;
                    position = touch.position;
                    return true;
                }
                return false;
            }
            if (pointerId != -1 || !Input.GetMouseButton(0)) return false;
            position = Input.mousePosition;
            return true;
        }

        public static bool HasBlockingGameControls(IList<RaycastResult> hits)
        {
            if (hits == null) return false;
            for (int i = 0; i < hits.Count; i++)
            {
                var go = hits[i].gameObject;
                if (go == null) continue;
                // Buttons within the computer's own UI remain usable. Gameplay
                // controls take precedence even if a large world canvas sorts first.
                if (go.GetComponentInParent<OS.OperatingSystem>() != null) continue;
                if (go.GetComponentInParent<Joystick>() != null ||
                    go.GetComponentInParent<ButtonHandler>() != null ||
                    go.GetComponentInParent<MobileHotbarButton>() != null ||
                    go.GetComponentInParent<Selectable>() != null)
                    return true;
            }
            return false;
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
