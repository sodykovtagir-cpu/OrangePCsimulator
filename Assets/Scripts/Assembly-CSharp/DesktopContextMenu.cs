using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
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
        
        private float pointerDownTime;
        private Vector2 pointerDownPos;
        private bool isPointerDown;
        private const float longPressDuration = 0.5f;
        private const float pointerMoveThreshold = 10f;
        
        private void Awake()
        {
            if (canvas == null)
                canvas = GetComponentInParent<Canvas>();
            if (operatingSystem == null)
                operatingSystem = GetComponentInParent<OperatingSystem>();
        }
        
        private void Update()
        {
            // Right-click on PC
            if (Input.GetMouseButtonDown(1))
            {
                ShowMenu(Input.mousePosition);
            }
            
            // Long press detection for mobile
            if (isPointerDown && Time.unscaledTime - pointerDownTime > longPressDuration)
            {
                if (Vector2.Distance(Input.mousePosition, pointerDownPos) < pointerMoveThreshold)
                {
                    ShowMenu(pointerDownPos);
                    isPointerDown = false;
                }
            }
        }
        
        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
                return; // Right-click handled in Update
            
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
            
            if (canvas == null || operatingSystem == null) return;
            
            menuPanel = CreateMenuPanel(screenPos, false);
            menuRect = menuPanel.GetComponent<RectTransform>();
            
            // Build menu items
            AddMenuItemWithSubmenu(menuPanel, "Упорядочить по", new[] {
                ("По названию", () => SortIcons(SortMode.Name)),
                ("По размеру", () => SortIcons(SortMode.Size)),
                ("По типу", () => SortIcons(SortMode.Type))
            });
            
            AddMenuItemWithSubmenu(menuPanel, "Создать", new[] {
                ("Текстовый документ", () => CreateFile("Новый документ.txt", "")),
                ("Папку", () => CreateFolder("Новая папка")),
                ("Lua-файл", () => CreateFile("script.lua", "-- Lua script\n"))
            });
            
            AddMenuItem(menuPanel, "Персонализация", () => OpenPersonalization());
            
            // Position menu
            PositionMenu(menuRect, screenPos, false);
        }
        
        private void CloseMenu()
        {
            if (menuPanel != null) Destroy(menuPanel);
            if (submenuPanel != null) Destroy(submenuPanel);
            menuPanel = null;
            submenuPanel = null;
            menuRect = null;
            submenuRect = null;
        }
        
        private GameObject CreateMenuPanel(Vector2 pos, bool isSubmenu)
        {
            var panel = new GameObject(isSubmenu ? "Submenu" : "ContextMenu");
            panel.transform.SetParent(canvas.transform, false);
            
            var rt = panel.AddComponent<RectTransform>();
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(200, 0); // Height auto
            
            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 2;
            vlg.padding = new RectOffset(4, 4, 4, 4);
            
            var csf = panel.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            var img = panel.AddComponent<Image>();
            img.color = new Color(0.95f, 0.95f, 0.95f, 0.98f);
            
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
            
            var le = item.AddComponent<LayoutElement>();
            le.preferredHeight = 30;
            
            var img = item.AddComponent<Image>();
            img.color = new Color(1, 1, 1, 0);
            
            var text = new GameObject("Text");
            text.transform.SetParent(item.transform, false);
            var trt = text.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(10, 0);
            trt.offsetMax = new Vector2(-10, 0);
            
            var txt = text.AddComponent<Text>();
            txt.text = label;
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.fontSize = 14;
            txt.color = Color.black;
            txt.alignment = TextAnchor.MiddleLeft;
            
            var btn = item.AddComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.7f, 0.85f, 1f, 1f);
            colors.pressedColor = new Color(0.5f, 0.7f, 0.9f, 1f);
            btn.colors = colors;
            
            btn.onClick.AddListener(() => {
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
            
            var le = item.AddComponent<LayoutElement>();
            le.preferredHeight = 30;
            
            var img = item.AddComponent<Image>();
            img.color = new Color(1, 1, 1, 0);
            
            var text = new GameObject("Text");
            text.transform.SetParent(item.transform, false);
            var trt = text.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(10, 0);
            trt.offsetMax = new Vector2(-30, 0);
            
            var txt = text.AddComponent<Text>();
            txt.text = label;
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.fontSize = 14;
            txt.color = Color.black;
            txt.alignment = TextAnchor.MiddleLeft;
            
            var arrow = new GameObject("Arrow");
            arrow.transform.SetParent(item.transform, false);
            var art = arrow.AddComponent<RectTransform>();
            art.anchorMin = new Vector2(1, 0.5f);
            art.anchorMax = new Vector2(1, 0.5f);
            art.sizeDelta = new Vector2(20, 20);
            art.anchoredPosition = new Vector2(-10, 0);
            
            var arrowTxt = arrow.AddComponent<Text>();
            arrowTxt.text = "▶";
            arrowTxt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            arrowTxt.fontSize = 10;
            arrowTxt.color = Color.black;
            arrowTxt.alignment = TextAnchor.MiddleCenter;
            
            var trigger = item.AddComponent<EventTrigger>();
            
            var enterEntry = new EventTrigger.Entry();
            enterEntry.eventID = EventTriggerType.PointerEnter;
            enterEntry.callback.AddListener((data) => {
                img.color = new Color(0.7f, 0.85f, 1f, 1f);
                ShowSubmenu(item, submenuItems);
            });
            trigger.triggers.Add(enterEntry);
            
            var exitEntry = new EventTrigger.Entry();
            exitEntry.eventID = EventTriggerType.PointerExit;
            exitEntry.callback.AddListener((data) => {
                img.color = new Color(1, 1, 1, 0);
                // Delay closing to allow moving to submenu
                Invoke(nameof(CloseSubmenuIfNotHovered), 0.1f);
            });
            trigger.triggers.Add(exitEntry);
        }
        
        private void ShowSubmenu(GameObject parentItem, (string label, System.Action action)[] items)
        {
            if (submenuPanel != null) Destroy(submenuPanel);
            
            submenuPanel = CreateMenuPanel(Vector2.zero, true);
            submenuRect = submenuPanel.GetComponent<RectTransform>();
            
            foreach (var (label, action) in items)
            {
                AddMenuItem(submenuPanel, label, action);
            }
            
            // Position submenu relative to parent item
            PositionSubmenu(parentItem);
        }
        
        private void CloseSubmenuIfNotHovered()
        {
            // Check if mouse is over submenu
            if (submenuPanel != null && submenuRect != null)
            {
                var pointer = new PointerEventData(EventSystem.current);
                pointer.position = Input.mousePosition;
                var results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, results);
                
                foreach (var result in results)
                {
                    if (result.gameObject.transform.IsChildOf(submenuPanel.transform) ||
                        result.gameObject == submenuPanel)
                        return; // Still hovering submenu
                }
                
                // Check if hovering parent menu item
                if (menuPanel != null)
                {
                    foreach (var result in results)
                    {
                        if (result.gameObject.transform.IsChildOf(menuPanel.transform))
                            return; // Hovering main menu
                    }
                }
            }
            
            if (submenuPanel != null) Destroy(submenuPanel);
            submenuPanel = null;
            submenuRect = null;
        }
        
        private void PositionMenu(RectTransform menu, Vector2 screenPos, bool isSubmenu)
        {
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform, screenPos, canvas.worldCamera, out localPoint);
            
            var canvasRect = canvas.transform as RectTransform;
            var canvasSize = canvasRect.rect.size;
            
            // Convert to anchored position
            var anchoredPos = localPoint;
            
            // Check if menu would go off-screen
            float menuWidth = 200;
            float menuHeight = menu.sizeDelta.y > 0 ? menu.sizeDelta.y : 120;
            
            // Right edge
            if (anchoredPos.x + menuWidth > canvasSize.x / 2)
                anchoredPos.x = canvasSize.x / 2 - menuWidth;
            
            // Bottom edge
            if (anchoredPos.y - menuHeight < -canvasSize.y / 2)
                anchoredPos.y = -canvasSize.y / 2 + menuHeight;
            
            menu.anchoredPosition = anchoredPos;
        }
        
        private void PositionSubmenu(GameObject parentItem)
        {
            if (submenuRect == null || menuRect == null) return;
            
            var parentRect = parentItem.GetComponent<RectTransform>();
            var parentPos = parentRect.position;
            
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform, parentPos, canvas.worldCamera, out localPoint);
            
            var canvasRect = canvas.transform as RectTransform;
            var canvasSize = canvasRect.rect.size;
            
            float submenuWidth = 200;
            float menuRight = menuRect.anchoredPosition.x + menuRect.rect.width;
            
            // Try to place submenu to the right of main menu
            float submenuX = menuRight + 5;
            
            // If no space on right, place on left
            if (submenuX + submenuWidth > canvasSize.x / 2)
                submenuX = menuRect.anchoredPosition.x - submenuWidth - 5;
            
            submenuRect.anchoredPosition = new Vector2(submenuX, localPoint.y);
        }
        
        private enum SortMode { Name, Size, Type }
        
        private void SortIcons(SortMode mode)
        {
            if (operatingSystem == null) return;
            operatingSystem.SortDesktopIcons(mode.ToString());
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
