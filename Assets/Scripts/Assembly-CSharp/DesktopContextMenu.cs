using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PC.Component.Software.OS;

namespace PC.Component.Software
{
    public class DesktopContextMenu : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public static DesktopContextMenu Instance { get; private set; }

        [SerializeField] private OperatingSystem operatingSystem;
        [SerializeField] private Canvas canvas;

        private GameObject menuPanel;
        private GameObject submenuPanel;
        private GameObject renamePanel;
        private RectTransform menuRect;
        private RectTransform submenuRect;
        private RectTransform canvasRect;

        private float pointerDownTime;
        private Vector2 pointerDownPos;
        private bool isPointerDown;

        private const float longPressDuration = PointerInput.LongPress;
        private const float pointerMoveThreshold = PointerInput.Slop;
        private const float menuWidth = 220f;
        private const float submenuOffset = 5f;
        private float lastMenuTime;

        private void Awake()
        {
            Instance = this;

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
        }

        private void OnDisable()
        {
            CloseMenu();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
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
            if (renamePanel != null && (go == renamePanel || go.transform.IsChildOf(renamePanel.transform)))
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

        private bool IsOverFileManager(Vector2 screenPos)
        {
            var results = RaycastAt(screenPos);
            for (int i = 0; i < results.Count; i++)
            {
                var go = results[i].gameObject;
                if (go != null && go.GetComponentInParent<FileManager>() != null)
                    return true;
            }

            return false;
        }

        private FileIcon FindFileIconAt(Vector2 screenPos)
        {
            var results = RaycastAt(screenPos);
            for (int i = 0; i < results.Count; i++)
            {
                var go = results[i].gameObject;
                if (go == null) continue;
                var icon = go.GetComponentInParent<FileIcon>();
                if (icon != null && icon.File != null)
                    return icon;
            }

            return null;
        }

        private bool CanOpenMenuAt(Vector2 screenPos)
        {
            var results = RaycastAt(screenPos);
            if (results.Count == 0)
                return false;

            bool sawDesktop = false;
            for (int i = 0; i < results.Count; i++)
            {
                var go = results[i].gameObject;
                if (go == null)
                    continue;

                if (IsMenuElement(go))
                    return false;

                if (go.GetComponentInParent<FileIcon>() != null)
                    return false;

                if (go.GetComponentInParent<FileManager>() != null)
                    return false;

                if (go.GetComponentInParent<App>() != null)
                    return false;

                if (operatingSystem != null && operatingSystem.BlocksDesktopMenu(go))
                    return false;

                if (go.transform == transform || go.transform.IsChildOf(transform))
                    sawDesktop = true;
                else if (operatingSystem != null && operatingSystem.IsDesktopContextTarget(go))
                    sawDesktop = true;
            }

            return sawDesktop;
        }

        private void OpenContextAt(Vector2 screenPos)
        {
            if (Time.unscaledTime - lastMenuTime < 0.05f)
                return;

            if (IsPointerOverMenu(screenPos))
                return;

            var icon = FindFileIconAt(screenPos);
            if (icon != null)
            {
                ShowFileMenu(icon.File, screenPos);
                return;
            }

            var results = RaycastAt(screenPos);
            FileManager explorer = null;
            ExplorerFileItem item = null;
            for (int i = 0; i < results.Count; i++)
            {
                var go = results[i].gameObject;
                if (go == null) continue;
                if (item == null)
                    item = go.GetComponentInParent<ExplorerFileItem>();
                if (explorer == null)
                    explorer = go.GetComponentInParent<FileManager>();
            }

            if (item != null && item.File != null)
            {
                ShowFileMenu(item.File, screenPos);
                return;
            }

            if (explorer != null)
            {
                ShowExplorerMenu(explorer, screenPos);
                return;
            }

            if (CanOpenMenuAt(screenPos))
                ShowDesktopMenu(screenPos, true);
        }

        private void Update()
        {
            if (menuPanel != null || renamePanel != null)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    CloseMenu();
                }
                else if (PointerInput.PressedThisFrame() && !IsPointerOverMenu(PointerInput.ScreenPosition()))
                {
                    CloseMenu();
                }
            }

            if (Input.GetMouseButtonDown(1))
            {
                OpenContextAt(Input.mousePosition);
                return;
            }

            if (PointerInput.PressedThisFrame())
            {
                pointerDownTime = Time.unscaledTime;
                pointerDownPos = PointerInput.ScreenPosition();
                isPointerDown = true;
            }

            if (!PointerInput.Held())
                isPointerDown = false;

            if (isPointerDown && Time.unscaledTime - pointerDownTime > longPressDuration)
            {
                if (Vector2.Distance(PointerInput.ScreenPosition(), pointerDownPos) < pointerMoveThreshold)
                {
                    isPointerDown = false;
                    PointerInput.ConsumedClick = true;
                    OpenContextAt(pointerDownPos);
                }
                else
                {
                    isPointerDown = false;
                }
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPointerDown = false;
        }

        public void ShowDesktopMenu(Vector2 screenPos)
        {
            ShowDesktopMenu(screenPos, false);
        }

        public void ShowDesktopMenu(Vector2 screenPos, bool skipDesktopCheck)
        {
            CloseMenu();
            lastMenuTime = Time.unscaledTime;

            if (!PrepareMenu(screenPos, skipDesktopCheck))
                return;

            AddMenuItem(menuPanel, "Автоупорядочить", AutoArrangeIcons);
            AddMenuItemWithSubmenu(menuPanel, "Упорядочить по", new (string, System.Action)[] {
                ("По названию", () => SortIcons(SortMode.Name)),
                ("По размеру", () => SortIcons(SortMode.Size)),
                ("По типу", () => SortIcons(SortMode.Type))
            });
            AddCreateSubmenu(menuPanel, "");
            if (operatingSystem != null && operatingSystem.HasClipboard)
                AddMenuItem(menuPanel, "Вставить", () => operatingSystem.PasteClipboard(""));
            AddMenuItem(menuPanel, "Обновить", RefreshDesktop);
            AddMenuItem(menuPanel, "Персонализация", OpenPersonalization);

            PositionMenu(menuRect, screenPos);
        }

        public void ShowFileMenu(File file, Vector2 screenPos)
        {
            if (file == null) return;
            CloseMenu();
            lastMenuTime = Time.unscaledTime;

            if (!PrepareMenu(screenPos, true))
                return;

            AddMenuItem(menuPanel, "Открыть", () =>
            {
                if (operatingSystem == null) return;
                operatingSystem.OpenFile(file);
            });

            if (!file.isFolder)
            {
                var apps = operatingSystem.GetOpenWithApps(file);
                if (apps != null && apps.Count > 0)
                {
                    var items = new List<(string, System.Action)>();
                    for (int i = 0; i < apps.Count; i++)
                    {
                        var app = apps[i];
                        if (app == null) continue;
                        items.Add((app.AppName, () => operatingSystem.OpenFileWith(file, app)));
                    }

                    if (items.Count > 0)
                        AddMenuItemWithSubmenu(menuPanel, "Открыть с помощью", items.ToArray());
                }
            }

            if (operatingSystem.CanCopyFile(file))
                AddMenuItem(menuPanel, "Скопировать", () => operatingSystem.CopyToClipboard(file));

            bool protect = operatingSystem.IsProtectedFile(file);
            if (!protect)
            {
                AddMenuItem(menuPanel, "Вырезать", () => operatingSystem.CutToClipboard(file));
                AddMenuItem(menuPanel, "Переименовать", () => ShowRenameDialog(file, screenPos), false);
                AddMenuItem(menuPanel, "Скрыть", () => operatingSystem.HideUserFile(file));
                AddMenuItem(menuPanel, "Удалить", () => operatingSystem.DeleteUserFile(file));
            }

            PositionMenu(menuRect, screenPos);
        }

        public void ShowExplorerMenu(FileManager explorer, Vector2 screenPos)
        {
            if (explorer == null) return;
            CloseMenu();

            if (!PrepareMenu(screenPos, true))
                return;

            string folder = explorer.CurrentFolder ?? "";
            AddCreateSubmenu(menuPanel, folder);
            if (operatingSystem != null && operatingSystem.HasClipboard)
                AddMenuItem(menuPanel, "Вставить", () => operatingSystem.PasteClipboard(folder));
            AddMenuItem(menuPanel, "Обновить", () => explorer.RefreshView());

            PositionMenu(menuRect, screenPos);
        }

        private void AddCreateSubmenu(GameObject parent, string folder)
        {
            AddMenuItemWithSubmenu(parent, "Создать", new (string, System.Action)[] {
                ("Текстовый документ", () => operatingSystem.CreateFileAt(folder, "Новый документ.txt", "")),
                ("Папку", () => operatingSystem.CreateFolderAt(folder, "Новая папка")),
                ("Lua-файл", () => operatingSystem.CreateFileAt(folder, "script.lua", "-- Lua script\n"))
            });
        }

        private bool PrepareMenu(Vector2 screenPos, bool skipDesktopCheck)
        {
            if (canvas == null || operatingSystem == null)
                return false;

            if (!skipDesktopCheck && !CanOpenMenuAt(screenPos))
                return false;

            menuPanel = CreateMenuPanel(false);
            menuRect = menuPanel.GetComponent<RectTransform>();
            return true;
        }

        public void CloseMenu()
        {
            CancelInvoke(nameof(CloseSubmenuIfNotHovered));

            if (menuPanel != null) Destroy(menuPanel);
            if (submenuPanel != null) Destroy(submenuPanel);
            if (renamePanel != null) Destroy(renamePanel);

            menuPanel = null;
            submenuPanel = null;
            renamePanel = null;
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

        private Font MenuFont()
        {
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void AddMenuItem(GameObject parent, string label, System.Action onClick, bool closeAfter = true)
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
            text.font = MenuFont();
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
                if (closeAfter)
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
            text.font = MenuFont();
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
            arrowText.font = MenuFont();
            arrowText.fontSize = 10;
            arrowText.color = Color.black;
            arrowText.alignment = TextAnchor.MiddleCenter;

            var button = item.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = image;
            button.onClick.AddListener(() => ShowSubmenu(item, submenuItems));

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

        private void ShowRenameDialog(File file, Vector2 screenPos)
        {
            CloseMenu();
            if (canvas == null || file == null || operatingSystem == null) return;

            renamePanel = new GameObject("RenameDialog");
            renamePanel.transform.SetParent(canvas.transform, false);
            renamePanel.transform.SetAsLastSibling();

            var rt = renamePanel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(240f, 78f);

            var image = renamePanel.AddComponent<Image>();
            image.color = new Color(0.95f, 0.95f, 0.95f, 0.98f);

            var outline = renamePanel.AddComponent<Outline>();
            outline.effectColor = new Color(0.3f, 0.3f, 0.3f, 1f);
            outline.effectDistance = new Vector2(1, -1);

            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(renamePanel.transform, false);
            var titleRt = titleGo.AddComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.sizeDelta = new Vector2(-16f, 22f);
            titleRt.anchoredPosition = new Vector2(0f, -6f);
            var title = titleGo.AddComponent<Text>();
            title.text = "Переименовать";
            title.font = MenuFont();
            title.fontSize = 14;
            title.color = Color.black;
            title.alignment = TextAnchor.MiddleLeft;

            var inputGo = new GameObject("Input");
            inputGo.transform.SetParent(renamePanel.transform, false);
            var inputRt = inputGo.AddComponent<RectTransform>();
            inputRt.anchorMin = new Vector2(0f, 0f);
            inputRt.anchorMax = new Vector2(1f, 0f);
            inputRt.pivot = new Vector2(0.5f, 0f);
            inputRt.sizeDelta = new Vector2(-16f, 28f);
            inputRt.anchoredPosition = new Vector2(0f, 10f);
            var inputImage = inputGo.AddComponent<Image>();
            inputImage.color = Color.white;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(inputGo.transform, false);
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(6f, 2f);
            textRt.offsetMax = new Vector2(-6f, -2f);
            var text = textGo.AddComponent<Text>();
            text.font = MenuFont();
            text.fontSize = 14;
            text.color = Color.black;
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = false;

            var input = inputGo.AddComponent<InputField>();
            input.textComponent = text;
            input.lineType = InputField.LineType.SingleLine;

            string display = file.path;
            int slash = display.LastIndexOf('/');
            if (slash >= 0) display = display.Substring(slash + 1);
            if (!file.isFolder)
            {
                var ext = File.Extension(display);
                display = File.NameWithoutExtension(display);
                input.text = display;
                input.onEndEdit.AddListener(value =>
                {
                    if (!string.IsNullOrEmpty(value))
                        operatingSystem.RenameUserFile(file, value + ext);
                    CloseMenu();
                });
            }
            else
            {
                input.text = display;
                input.onEndEdit.AddListener(value =>
                {
                    if (!string.IsNullOrEmpty(value))
                        operatingSystem.RenameUserFile(file, value);
                    CloseMenu();
                });
            }

            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, GetEventCamera(), out localPoint);
            rt.anchoredPosition = localPoint;
            input.ActivateInputField();
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

        private void OpenPersonalization()
        {
            if (operatingSystem == null) return;
            if (!operatingSystem.TryLaunchApp("Personalization"))
                operatingSystem.ShowRequires("Personalization");
        }
    }
}
