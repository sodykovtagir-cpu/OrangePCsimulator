using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PC.Component.Software.Sites
{
    /// <summary>UGUI renderer for orange-site data; never interprets HTML.</summary>
    public sealed class SiteCanvas : MonoBehaviour
    {
        SiteDocument document;
        SiteLua lua;
        RectTransform content, design;
        readonly Dictionary<string, RectTransform> nodes = new Dictionary<string, RectTransform>();
        readonly Dictionary<string, Outline> outlines = new Dictionary<string, Outline>();
        readonly Dictionary<string, Text> labels = new Dictionary<string, Text>();
        readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        public Action<string> Selected, Redirect, Message;
        public Action<SiteAsset> Download;
        public Action<string, float, float> Moved;
        public Action<string, string> TextEdited;
        public bool Editing { get; private set; }

        public static SiteCanvas Create(Transform parent)
        {
            var go = SiteWidgets.Panel("Site canvas", parent, Color.white);
            go.AddComponent<RectMask2D>(); return go.AddComponent<SiteCanvas>();
        }
        public void Render(SiteDocument doc, bool editing)
        {
            doc.Validate(); document = doc; Editing = editing;
            SiteWidgets.Clear(transform); DisposeTextures(); nodes.Clear(); labels.Clear(); outlines.Clear();
            var body = SiteWidgets.Node("Scroll content", transform); content = (RectTransform)body.transform;
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(0, 1);
            content.anchoredPosition = Vector2.zero;
            var sheet = SiteWidgets.Panel("Page", content, SiteWidgets.ColorValue(doc.background));
            design = SiteWidgets.Box(sheet, 0, 0, doc.width, doc.height);
            var scroll = GetComponent<ScrollRect>(); if (scroll == null) scroll = gameObject.AddComponent<ScrollRect>();
            scroll.viewport = (RectTransform)transform; scroll.content = content;
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.StopMovement(); scroll.verticalNormalizedPosition = 1;
            lua = new SiteLua(doc);
            lua.Redirect = url => Redirect?.Invoke(url); lua.Download = asset => Download?.Invoke(asset);
            lua.Message = text => Message?.Invoke(text);
            lua.TextChanged = (id, text) => { Text label; if (labels.TryGetValue(id, out label)) label.text = text; };
            lua.VisibilityChanged = (id, visible) => { RectTransform node; if (nodes.TryGetValue(id, out node)) node.gameObject.SetActive(visible); };
            foreach (var element in doc.elements) Draw(element);
            Resize();
        }
        void Draw(SiteElement element)
        {
            var go = SiteWidgets.Node(element.name, design); var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(element.pivotX, 1 - element.pivotY);
            rect.anchoredPosition = new Vector2(element.x, -element.y); rect.sizeDelta = new Vector2(element.width, element.height);
            nodes[element.id] = rect;
            if (element.kind != "text")
            {
                var image = go.AddComponent<Image>(); image.color = SiteWidgets.ColorValue(element.fill);
                if (!string.IsNullOrEmpty(element.image)) image.sprite = Image(element.image);
                image.preserveAspect = element.kind == "image";
            }
            if (element.kind != "image")
            {
                var label = SiteWidgets.Label(go.transform, element.text, element.fontSize);
                label.font = SiteWidgets.Font(element.font); label.color = SiteWidgets.ColorValue(element.textColor);
                label.alignment = element.kind == "button" ? TextAnchor.MiddleCenter : TextAnchor.UpperLeft;
                label.raycastTarget = Editing;
                labels[element.id] = label;
            }
            if (Editing)
            {
                var outline = (element.kind == "text" ? labels[element.id].gameObject : go).AddComponent<Outline>();
                outlines[element.id] = outline; outline.effectColor = new Color(1, .45f, 0); outline.effectDistance = new Vector2(2, 2); outline.enabled = false;
                var handle = go.AddComponent<SiteElementHandle>(); handle.Bind(this, element.id, design, element.kind == "text");
            }
            else if (element.kind == "button")
            {
                var button = go.AddComponent<Button>(); button.targetGraphic = go.GetComponent<Image>();
                button.onClick.AddListener(() =>
                {
                    try { lua.Click(element.id); }
                    catch (Exception ex) { Message?.Invoke("Lua: " + ex.Message); }
                });
            }
        }
        Sprite Image(string name)
        {
            Sprite sprite; if (sprites.TryGetValue(name, out sprite)) return sprite;
            var asset = document.Asset(name); var bytes = asset.Bytes(); SiteDocument.ValidateImage(bytes, asset.media);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes)) { Destroy(texture); throw new ArgumentException("Cannot decode image " + name); }
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
            sprites[name] = sprite; return sprite;
        }
        void LateUpdate() { Resize(); }
        void Resize()
        {
            if (design == null || content == null || document == null) return;
            var viewport = (RectTransform)transform; float scale = Mathf.Max(.01f, viewport.rect.width / document.width);
            design.localScale = new Vector3(scale, scale, 1);
            content.sizeDelta = new Vector2(0, Mathf.Max(viewport.rect.height, document.height * scale));
        }
        public void Select(string id)
        {
            foreach (var entry in outlines) if (entry.Value != null) entry.Value.enabled = entry.Key == id;
            Selected?.Invoke(id);
        }
        public void Move(string id, Vector2 position) { Moved?.Invoke(id, position.x, -position.y); }
        public void EditText(string id)
        {
            RectTransform node; if (!Editing || !nodes.TryGetValue(id, out node) || node.Find("Inline edit") != null) return;
            var element = document.Find(id); if (element == null || element.kind != "text") return;
            var input = SiteWidgets.Input(node, element.text, value => TextEdited?.Invoke(id, value), true);
            input.gameObject.name = "Inline edit"; input.textComponent.fontSize = element.fontSize;
            input.textComponent.font = SiteWidgets.Font(element.font); input.textComponent.color = SiteWidgets.ColorValue(element.textColor);
            input.characterLimit = 4096; input.ActivateInputField(); input.MoveTextEnd(false);
        }
        void DisposeTextures()
        {
            foreach (var sprite in sprites.Values) if (sprite != null) { Destroy(sprite.texture); Destroy(sprite); }
            sprites.Clear();
        }
        void OnDestroy() { DisposeTextures(); }
    }
}
