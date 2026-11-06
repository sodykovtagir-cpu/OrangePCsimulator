using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PC.Component.Software
{
    // Attached to the authored FavoritesItemPrefab; no duplicate tiles or modal UI.
    public sealed class FavoriteTile : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IPointerExitHandler, IPointerClickHandler
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private Text titleText;
        [SerializeField] private Button deleteButton;

        private string url;
        private Action<string> open;
        private Action<string> remove;
        private int pointerId;
        private float downTime;
        private Vector2 downPosition;
        private bool pressing;
        private bool suppressClick;
        private bool canceledClick;

        public void Initialize(string address, Sprite icon, Action<string> openSite, Action<string> removeSite)
        {
            url = address;
            open = openSite;
            remove = removeSite;
            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.preserveAspect = true;
            }
            if (titleText != null)
            {
                var host = address.Split('/')[0];
                titleText.text = host.Length > 12 ? host.Substring(0, 11) + "…" : host;
            }
            if (deleteButton != null)
            {
                deleteButton.gameObject.SetActive(false);
                deleteButton.onClick.AddListener(Remove);
            }
        }

        private void Remove()
        {
            if (remove != null) remove(url);
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (!PointerInput.IsPrimary(data)) return;
            pointerId = data.pointerId;
            downPosition = data.position;
            downTime = Time.unscaledTime;
            suppressClick = false;
            canceledClick = false;
            pressing = true;
        }

        private void Update()
        {
            if (!pressing) return;
            Vector2 position;
            if (!PointerInput.TryGetHeldPosition(pointerId, out position) ||
                Vector2.Distance(position, downPosition) >= PointerInput.Slop)
            {
                pressing = false;
                canceledClick = true;
                return;
            }
            if (Time.unscaledTime - downTime >= PointerInput.LongPress)
                RevealDelete();
        }

        private void RevealDelete()
        {
            pressing = false;
            suppressClick = true;
            if (deleteButton != null) deleteButton.gameObject.SetActive(true);
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (data != null && data.pointerId != pointerId) return;
            // A release between two Update calls must still count as a hold.
            if (pressing && data != null &&
                Time.unscaledTime - downTime >= PointerInput.LongPress &&
                Vector2.Distance(data.position, downPosition) < PointerInput.Slop)
                RevealDelete();
            pressing = false;
        }

        public void OnPointerExit(PointerEventData data)
        {
            if (data != null && data.pointerId == pointerId && pressing)
            {
                pressing = false;
                canceledClick = true;
            }
        }

        public void OnPointerClick(PointerEventData data)
        {
            if (data == null || data.button != PointerEventData.InputButton.Left) return;
            if (suppressClick || canceledClick || data.dragging)
            {
                suppressClick = false;
                canceledClick = false;
                return;
            }
            if (open != null) open(url);
        }

        private void OnDisable()
        {
            pressing = false;
            suppressClick = false;
            canceledClick = false;
            if (deleteButton != null) deleteButton.gameObject.SetActive(false);
        }
    }
}
