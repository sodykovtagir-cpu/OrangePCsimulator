using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PC.Component.Software
{
    /// <summary>Editable list-row template; DeviceManager supplies only text and selection state.</summary>
    public class DeviceManagerRow : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        [SerializeField] private Image background;
        [SerializeField] private Text label;
        private DeviceManager manager;
        private int systemId;
        private float downTime;
        private Vector2 downPosition;
        private int pointerId;
        private bool held;
        private bool menuOpened;

        public void Init(DeviceManager owner, int id, string caption, bool selected, bool alternate)
        {
            manager = owner;
            systemId = id;
            if (label != null) label.text = (selected ? "[x] " : "[ ] ") + caption;
            if (background != null)
                background.color = selected ? owner.SelectedRowColor :
                    alternate ? owner.AlternateRowColor : Color.white;
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (!PointerInput.IsPrimary(data)) return;
            var menu = DesktopContextMenu.For(this);
            if (menu != null && !menu.CanReceivePointer(data.position)) return;
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
            if (data.button == PointerEventData.InputButton.Right)
                manager.ShowRowMenu(systemId, data.position);
            else if (PointerInput.IsPrimary(data) && !menuOpened && !PointerInput.ConsumedClick)
                manager.ToggleRow(systemId);
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
            manager.ShowRowMenu(systemId, downPosition);
        }

        private void OnDisable() { held = false; }
    }

}
