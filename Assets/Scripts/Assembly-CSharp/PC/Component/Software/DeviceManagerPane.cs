using UnityEngine;
using UnityEngine.EventSystems;

namespace PC.Component.Software
{
    /// <summary>Right-click / long-press on the empty list, like File Manager's ExplorerPane.</summary>
    public class DeviceManagerPane : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        private DeviceManager manager;
        private float downTime;
        private Vector2 downPosition;
        private int pointerId;
        private bool held;
        private bool menuOpened;

        public void Init(DeviceManager owner) { manager = owner; }

        public void OnPointerDown(PointerEventData data)
        {
            if (!PointerInput.IsPrimary(data)) return;
            var menu = DesktopContextMenu.For(this);
            if (menu != null && !menu.CanReceiveAppPointer(manager, data.position)) return;
            held = true;
            menuOpened = false;
            pointerId = data.pointerId;
            downTime = Time.unscaledTime;
            downPosition = data.position;
            PointerInput.ConsumedClick = false;
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (data == null || data.pointerId == pointerId) held = false;
        }

        public void OnPointerClick(PointerEventData data)
        {
            if (manager == null || data == null) return;
            var menu = DesktopContextMenu.For(this);
            if (menu != null && !menu.CanReceiveAppPointer(manager, data.position)) return;
            if (data.button == PointerEventData.InputButton.Right)
                manager.ShowQuickMenu(data.position);
            else if (PointerInput.IsPrimary(data) && !menuOpened && !PointerInput.ConsumedClick)
                manager.ClearSelection();
        }

        private void Update()
        {
            if (!held || manager == null || menuOpened) return;
            Vector2 current;
            if (!PointerInput.TryGetHeldPosition(pointerId, out current) ||
                Vector2.Distance(current, downPosition) >= PointerInput.Slop)
            { held = false; return; }
            if (Time.unscaledTime - downTime <= PointerInput.LongPress) return;
            held = false;
            menuOpened = true;
            PointerInput.ConsumedClick = true;
            manager.ShowQuickMenu(downPosition);
        }

        private void OnDisable() { held = false; }
    }
}
