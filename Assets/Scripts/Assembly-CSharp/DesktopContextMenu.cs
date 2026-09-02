using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PC.Component.Software.OS;

namespace PC.Component.Software
{
    public class DesktopContextMenu : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private OperatingSystem operatingSystem;
        [SerializeField] private Canvas canvas;

        private GameObject menuPanel;
        private GameObject submenuPanel;
        private RectTransform menuRect;
        private RectTransform submenuRect;
        private RectTransform canvasRect;

        private float pointerDownTime;
        private Vector2 pointerDownPos;
        private bool isPointerDown;

        private const float longPressDuration = 0.5f;
        private const float pointerMoveThreshold = 10f;
        private const float menuWidth = 200f;
        private const float submenuOffset = 5f;

        private void Awake()
        {
            if (canvas == null)
                canvas = GetComponentInParent<Canvas>();

            if (operatingSystem == null)
                operatingSystem = GetComponentInParent<OperatingSystem>();

            if (operatingSystem == null)
                operatingSystem = FindObjectOfType<OperatingSystem>();

            if (canvas == null)
            {
                var canvases = FindObjectsOfType<Canvas>();
                foreach (var c in canvases)
                {
                    if (c.GetComponent<OperatingSystem>() != null || c.GetComponentInChildren<OperatingSystem>() != null)
                    {
                        canvas = c;
                        break;
                    }
                }

                if (canvas == null && canvases.Length > 0)
                    canvas = canvases[0];
            }

            canvasRect = canvas != null ? canvas.transform as RectTransform : null;
            Debug.Log($"[DesktopContextMenu] Awake: canvas={canvas?.name ?? "NULL"}, OS={operatingSystem?.name ?? "NULL"}");
        }

        private void OnDisable()
        {
            CloseMenu();
        }

        private void OnDestroy()
        {
            CloseMenu();
        }

        private Camera GetEventCamera()
        {
            if (canvas == null) return null;
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
            return canvas.worldCamera;
        }

        private List<RaycastResult> RaycastAt(Vector2 screenPos)
        {
            var results = new List<RaycastResult>();
            if (EventSystem.current == null)
                return results;

            var pointer = new PointerEventData(EventSystem.current);
            pointer.position = screenPos;
            EventSystem.current.RaycastAll(pointer, results);
            return results;
        }

        private bool IsMenuElement(GameObject go)
        {
            if (go == null) return false;
            if (menuPanel != null && (go == menuPanel || go.transform.IsChildOf(menuPanel.transform)))
                return true;
            if (submenuPanel != null && (go == submenuPanel || go.transform.IsChildOf(submenuPanel.transform)))
                return true;
            return false;
        }

        private bool IsPointerOverMenu(Vector2 screenPos)
        {
            var results = RaycastAt(screenPos);
            for (int i = 0; i < results.Count; i++)
            {
                if (IsMenuElement(results[i].gameObject))
                    return true;
            }

            return false;
        }

        private bool CanOpenMenuAt(Vector2 screenPos)
        {
            var results = RaycastAt(screenPos);
            if (results.Count == 0)
                return false;

            for (int i = 0; i < results.Count; i++)
            {
                var go = results[i].gameObject;
                if (go == null)
                    continue;

                if (IsMenuElement(go))
                    return false;

                if (go.GetComponentInParent<FileIcon>() != null)
                    return false;

                if (go.transform == transform || go.transform.IsChildOf(transform))
                    return true;

                if (operatingSystem != null && go.transform.IsChildOf(operatingSystem.transform))
                    return false;
            }

            return false;
        }

        private void Update()
        {
            if (menuPanel != null)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    CloseMenu();
                }
                else if (Input.GetMouseButtonDown(0) && !IsPointerOverMenu(Input.mousePosition))
                {
                    CloseMenu();
                }
            }

            if (Input.GetMouseButtonDown(1))
            {
                Debug.Log("[DesktopContextMenu] Right-click detected");
                if (CanOpenMenuAt(Input.mousePosition))
                    ShowMenu(Input.mousePosition);
                else
                    CloseMenu();
            }

            if (isPointerDown && Time.unscaledTime - pointerDownTime > longPressDuration)
            {
                if (Vector2.Distance(Input.mousePosition, pointerDownPos) < pointerMoveThreshold && CanOpenMenuAt(pointerDownPos))
                {
                    ShowMenu(pointerDownPos);
                    isPointerDown = false;
                }
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null || eventData.button == PointerEventData.InputButton.Right)
                return;

            if (!CanOpenMenuAt(eventData.position))
            {
                isPointerDown = false;
                return;
            }

            pointerDownTime = Time.unscaledTime;
            pointerDownPos = eventData.position;
            isPointerDown = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPointerDown = false;
        }

        private void ShowMenu(Vector2 screenPos)
        {
            CloseMenu();

            if (canvas == null)
            {
                Debug.LogError("[DesktopContextMenu] Cannot show menu: canvas is null!");
                return;
            }

            if (operatingSystem == null)
            {
                Debug.LogError("[DesktopContextMenu] Cannot show menu: operatingSystem is null!");
                return;
            }

            if (!CanOpenMenuAt(screenPos))
                return;

            Debug.Log($"[DesktopContextMenu] Creating menu at {screenPos}, canvas={canvas.name}");

            menuPanel = CreateMenuPanel(false);
            menuRect = menuPanel.GetComponent<RectTransform>();

            AddMenuItem(menuPanel, "Автоупорядочить", AutoArrangeIcons);
            AddMenuItemWithSubmenu(menuPanel, "Упорядочить по", new (string, System.Action)[] {
                ("По названию", () => SortIcons(SortMode.Name)),
                ("По размеру", () => SortIcons(SortMode.Size)),
                ("По типу", () => SortIcons(SortMode.Type))
            });
            AddMenuItemWithSubmenu(menuPanel, "Создать", new (string, System.Action)[] {
                ("Текстовый документ", () => CreateFile("Новый документ.txt", "")),
                ("Папку", () => CreateFolder("Новая папка")),
                ("Lua-файл", () => CreateFile("script.lua", "-- Lua script\n"))
            });
            AddMenuItem(menuPanel, "Обновить", RefreshDesktop);
            AddMenuItem(menuPanel, "Персонализация", OpenPersonalization);

            PositionMenu(menuRect, screenPos);
        }

        private void CloseMenu()
        {
            CancelInvoke(nameof(CloseSubmenuIfNotHovered));

            if (menuPanel != null) Destroy(menuPanel);
            if (submenuPanel != null) Destroy(submenuPanel);

            menuPanel = null;
            submenuPanel = null;
            menuRect = null;
            submenuRect = null;
        }

        private GameObject CreateMenuPanel(bool isSubmenu)
        {
            var panel = new GameObject(isSubmenu ? "Submenu" : "ContextMenu");
            panel.transform.SetParent(canvas.transform, false);
            panel.transform.SetAsLastSibling();

            var rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(menuWidth, 0f);

            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 2;
            layout.padding = new RectOffset(4, 4, 4, 4);

            var fitter = panel.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var image = panel.AddComponent<Image>();
            image.color = new Color(0.95f, 0.95f, 0.95f, 0.98f);

            var outline = panel.AddComponent<Outline>();
            outline.effectColor = new Color(0.3f, 0.3f, 0.3f, 1f);
            outline.effectDistance = new Vector2(1, -1);

            return panel;
        }

        private void AddMenuItem(GameObject parent, string label, System.Action onClick)
        {
            var item = new GameObject("MenuItem");
            item.transform.SetParent(parent.transform, false);

            var rt = item.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 30);

            var layoutElement = item.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 30;

            var image = item.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(item.transform, false);

            var textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(10f, 0f);
            textRt.offsetMax = new Vector2(-10f, 0f);

            var text = textGo.AddComponent<Text>();
            text.text = label;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 14;
            text.color = Color.black;
            text.alignment = TextAnchor.MiddleLeft;

            var button = item.AddComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;
            button.targetGraphic = image;

            var colors = button.colors;
            colors.highlightedColor = new Color(0.7f, 0.85f, 1f, 1f);
            colors.pressedColor = new Color(0.5f, 0.7f, 0.9f, 1f);
            button.colors = colors;

            button.onClick.AddListener(() =>
            {
                onClick?.Invoke();
                CloseMenu();
            });
        }

        private void AddMenuItemWithSubmenu(GameObject parent, string label, (string label, System.Action action)[] submenuItems)
        {
            var item = new GameObject("MenuItemWithSubmenu");
            item.transform.SetParent(parent.transform, false);

            var rt = item.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 30);

            var layoutElement = item.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 30;

            var image = item.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(item.transform, false);

            var textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(10f, 0f);
            textRt.offsetMax = new Vector2(-30f, 0f);

            var text = textGo.AddComponent<Text>();
            text.text = label;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 14;
            text.color = Color.black;
            text.alignment = TextAnchor.MiddleLeft;

            var arrowGo = new GameObject("Arrow");
            arrowGo.transform.SetParent(item.transform, false);

            var arrowRt = arrowGo.AddComponent<RectTransform>();
            arrowRt.anchorMin = new Vector2(1f, 0.5f);
            arrowRt.anchorMax = new Vector2(1f, 0.5f);
            arrowRt.sizeDelta = new Vector2(20f, 20f);
            arrowRt.anchoredPosition = new Vector2(-10f, 0f);

            var arrowText = arrowGo.AddComponent<Text>();
            arrowText.text = "▶";
            arrowText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            arrowText.fontSize = 10;
            arrowText.color = Color.black;
            arrowText.alignment = TextAnchor.MiddleCenter;

            var trigger = item.AddComponent<EventTrigger>();

            var enterEntry = new EventTrigger.Entry();
            enterEntry.eventID = EventTriggerType.PointerEnter;
            enterEntry.callback.AddListener((data) =>
            {
                image.color = new Color(0.7f, 0.85f, 1f, 1f);
                ShowSubmenu(item, submenuItems);
            });
            trigger.triggers.Add(enterEntry);

            var exitEntry = new EventTrigger.Entry();
            exitEntry.eventID = EventTriggerType.PointerExit;
            exitEntry.callback.AddListener((data) =>
            {
                image.color = new Color(1f, 1f, 1f, 0f);
                Invoke(nameof(CloseSubmenuIfNotHovered), 0.1f);
            });
            trigger.triggers.Add(exitEntry);
        }

        private void ShowSubmenu(GameObject parentItem, (string label, System.Action action)[] items)
        {
            CancelInvoke(nameof(CloseSubmenuIfNotHovered));

            if (submenuPanel != null) Destroy(submenuPanel);

            submenuPanel = CreateMenuPanel(true);
            submenuRect = submenuPanel.GetComponent<RectTransform>();

            for (int i = 0; i < items.Length; i++)
                AddMenuItem(submenuPanel, items[i].label, items[i].action);

            PositionSubmenu(parentItem);
        }

        private void CloseSubmenuIfNotHovered()
        {
            if (EventSystem.current == null)
                return;

            var results = RaycastAt(Input.mousePosition);
            for (int i = 0; i < results.Count; i++)
            {
                if (IsMenuElement(results[i].gameObject))
                    return;
            }

            if (submenuPanel != null) Destroy(submenuPanel);
            submenuPanel = null;
            submenuRect = null;
        }

        private void PositionMenu(RectTransform menu, Vector2 screenPos)
        {
            if (menu == null || canvasRect == null)
                return;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(menu);

            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, GetEventCamera(), out localPoint);

            var canvasSize = canvasRect.rect.size;
            var anchoredPos = localPoint;

            float width = menu.rect.width > 0f ? menu.rect.width : menuWidth;
            float height = menu.rect.height > 0f ? menu.rect.height : 120f;

            if (anchoredPos.x + width > canvasSize.x / 2f)
                anchoredPos.x = canvasSize.x / 2f - width;
            if (anchoredPos.x < -canvasSize.x / 2f)
                anchoredPos.x = -canvasSize.x / 2f;

            if (anchoredPos.y - height < -canvasSize.y / 2f)
                anchoredPos.y = -canvasSize.y / 2f + height;
            if (anchoredPos.y > canvasSize.y / 2f)
                anchoredPos.y = canvasSize.y / 2f;

            menu.anchoredPosition = anchoredPos;
        }

        private void PositionSubmenu(GameObject parentItem)
        {
            if (submenuRect == null || parentItem == null || canvasRect == null)
                return;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(submenuRect);

            var parentRect = parentItem.GetComponent<RectTransform>();
            if (parentRect == null)
                return;

            var corners = new Vector3[4];
            parentRect.GetWorldCorners(corners);

            Vector2 topLeftLocal;
            Vector2 topRightLocal;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                RectTransformUtility.WorldToScreenPoint(GetEventCamera(), corners[1]),
                GetEventCamera(),
                out topLeftLocal);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                RectTransformUtility.WorldToScreenPoint(GetEventCamera(), corners[2]),
                GetEventCamera(),
                out topRightLocal);

            var canvasSize = canvasRect.rect.size;
            float width = submenuRect.rect.width > 0f ? submenuRect.rect.width : menuWidth;
            float height = submenuRect.rect.height > 0f ? submenuRect.rect.height : 120f;

            float x = topRightLocal.x + submenuOffset;
            if (x + width > canvasSize.x / 2f)
                x = topLeftLocal.x - width - submenuOffset;

            float y = topLeftLocal.y;
            if (y - height < -canvasSize.y / 2f)
                y = -canvasSize.y / 2f + height;
            if (y > canvasSize.y / 2f)
                y = canvasSize.y / 2f;

            submenuRect.anchoredPosition = new Vector2(x, y);
        }

        private enum SortMode { Name, Size, Type }

        private void SortIcons(SortMode mode)
        {
            if (operatingSystem == null) return;
            operatingSystem.SortDesktopIcons(mode.ToString());
        }

        private void AutoArrangeIcons()
        {
            if (operatingSystem == null) return;
            operatingSystem.AutoArrangeIcons();
        }

        private void RefreshDesktop()
        {
            if (operatingSystem == null) return;
            operatingSystem.RefreshDesktopIcon();
        }

        private void CreateFile(string name, string content)
        {
            if (operatingSystem == null) return;
            operatingSystem.CreateDesktopFile(name, content);
        }

        private void CreateFolder(string name)
        {
            if (operatingSystem == null) return;
            operatingSystem.CreateDesktopFolder(name);
        }

        private void OpenPersonalization()
        {
            if (operatingSystem == null) return;
            operatingSystem.TryLaunchApp("Personalization");
        }
    }
}
