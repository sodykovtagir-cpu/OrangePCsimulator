using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace PC.Component.Software.Sites
{
    // This is data, never HTML, a prefab, a CLR type name or an executable.
    [Serializable]
    public sealed class SiteDocument
    {
        public const int MaxJsonBytes = 8 * 1024 * 1024;
        public const int MaxElements = 256;
        public const int MaxAssets = 64;
        public const int MaxAssetBytes = 2 * 1024 * 1024;
        public const int MaxScriptLength = 8192;
        public string format = "orange-site";
        public int version = 1;
        public string title = "New site";
        public string description = "";
        public string keywords = "";
        public string icon = "";
        public float width = 960, height = 720;
        public string background = "#FFFFFF";
        public List<SiteElement> elements = new List<SiteElement>();
        // Flat, virtual site root. No operating-system paths or external URLs.
        public List<SiteAsset> assets = new List<SiteAsset>();

        public static SiteDocument Parse(string json)
        {
            if (string.IsNullOrEmpty(json) || Encoding.UTF8.GetByteCount(json) > MaxJsonBytes)
                throw new ArgumentException("Site package is empty or too large");
            ValidateJsonDepth(json);
            SiteDocument doc;
            try
            {
                var header = JsonUtility.FromJson<SiteHeader>(json);
                if (header == null || header.format != "orange-site" || header.version != 1)
                    throw new ArgumentException("Unsupported site format/version");
                doc = JsonUtility.FromJson<SiteDocument>(json);
            }
            catch (Exception) { throw new ArgumentException("Invalid site JSON"); }
            if (doc == null) throw new ArgumentException("Invalid site package");
            doc.Validate();
            return doc;
        }

        [Serializable]
        sealed class SiteHeader { public string format; public int version; }

        static void ValidateJsonDepth(string json)
        {
            int depth = 0; bool quoted = false, escaped = false;
            foreach (char c in json)
            {
                if (quoted)
                {
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') quoted = false;
                }
                else if (c == '"') quoted = true;
                else if (c == '{' || c == '[') { if (++depth > 32) throw new ArgumentException("Site JSON is too deeply nested"); }
                else if (c == '}' || c == ']') { if (--depth < 0) throw new ArgumentException("Invalid site JSON"); }
            }
            if (depth != 0 || quoted) throw new ArgumentException("Invalid site JSON");
        }

        public string ToJson()
        {
            Validate();
            string json = JsonUtility.ToJson(this);
            if (Encoding.UTF8.GetByteCount(json) > MaxJsonBytes) throw new ArgumentException("Site package is too large");
            return json;
        }

        public void Validate()
        {
            if (format != "orange-site" || version != 1) throw new ArgumentException("Unsupported site format/version");
            Text(title, 120, "Title"); Text(description, 1024, "Description"); Text(keywords, 512, "Keywords");
            Range(width, 128, 4096); Range(height, 128, 16384); ValidateColor(background);
            if (elements == null || elements.Count > MaxElements || assets == null || assets.Count > MaxAssets)
                throw new ArgumentException("Too many elements/assets");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int total = 0, pixels = 0;
            foreach (var asset in assets)
            {
                if (asset == null || !SafeName(asset.name) || !names.Add(asset.name)) throw new ArgumentException("Invalid or duplicate asset name");
                if (asset.media != "image/png" && asset.media != "image/jpeg" && asset.media != "application/x-pcos-file")
                    throw new ArgumentException("Unsupported site asset");
                var bytes = asset.Bytes(); total += bytes.Length;
                if (total > 6 * 1024 * 1024) throw new ArgumentException("Site assets are too large");
                if (asset.media.StartsWith("image/", StringComparison.Ordinal))
                {
                    pixels += ValidateImage(bytes, asset.media);
                    if (pixels > 8 * 1024 * 1024) throw new ArgumentException("Too many decoded image pixels (8 megapixels maximum)");
                }
            }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var element in elements)
            {
                if (element == null || !SafeName(element.id) || !ids.Add(element.id)) throw new ArgumentException("Invalid or duplicate element ID");
                if (element.kind != "button" && element.kind != "text" && element.kind != "image") throw new ArgumentException("Unknown element type");
                Text(element.name, 80, "Name"); Text(element.text, 4096, "Text"); Text(element.onClick, MaxScriptLength, "OnClick");
                Range(element.x, -16384, 16384); Range(element.y, -16384, 16384);
                Range(element.width, 1, 4096); Range(element.height, 1, 16384);
                Range(element.pivotX, 0, 1); Range(element.pivotY, 0, 1); Range(element.fontSize, 6, 256);
                if (element.font != "default" && element.font != "mono" && element.font != "medium") throw new ArgumentException("Unknown font");
                ValidateColor(element.fill); ValidateColor(element.textColor);
                if (!string.IsNullOrEmpty(element.image)) ImageAsset(element.image);
                if (element.actions == null) element.actions = new List<SiteAction>(); // legacy v1 files
                if (element.actions.Count > 32) throw new ArgumentException("Too many button actions");
                foreach (var action in element.actions) SiteCommands.Validate(action);
                if (element.kind != "button" && (!string.IsNullOrEmpty(element.onClick) || element.actions.Count > 0)) throw new ArgumentException("OnClick belongs to a button");
            }
            if (!string.IsNullOrEmpty(icon)) ImageAsset(icon);
        }

        public SiteElement Find(string id) { return elements.Find(e => e.id == id); }
        public SiteAsset Asset(string name) { return assets.Find(a => string.Equals(a.name, name, StringComparison.Ordinal)); }
        void ImageAsset(string name)
        {
            var asset = Asset(name);
            if (asset == null || !asset.media.StartsWith("image/", StringComparison.Ordinal)) throw new ArgumentException("Missing image: " + name);
        }
        public static bool SafeName(string name)
        {
            return !string.IsNullOrEmpty(name) && name.Length <= 80 && name != "." && name != ".." &&
                Regex.IsMatch(name, "^[A-Za-z0-9_-][A-Za-z0-9_.-]*$");
        }
        public static string NormalizeUrl(string value)
        {
            value = (value ?? "").Trim().ToLowerInvariant();
            if (value.StartsWith("https://", StringComparison.Ordinal)) value = value.Substring(8);
            else if (value.StartsWith("http://", StringComparison.Ordinal)) value = value.Substring(7);
            value = value.TrimEnd('/');
            // In-game address only. Never open a native browser or make HTTP to it.
            if (value.Length > 160 || !Regex.IsMatch(value, @"^(?:www\.)?[a-z0-9](?:[a-z0-9.-]*[a-z0-9])?\.[a-z]{2,}(?:/[a-z0-9_./-]*)?$") || value.Contains(".."))
                throw new ArgumentException("Invalid game URL");
            return value;
        }
        static void Text(string s, int max, string name)
        {
            if (s == null || s.Length > max) throw new ArgumentException(name + " is too long or null");
        }
        static void Range(float n, float min, float max)
        {
            if (float.IsNaN(n) || float.IsInfinity(n) || n < min || n > max) throw new ArgumentException("Value outside site limits");
        }
        public static void ValidateColor(string value)
        {
            if (value == null || !Regex.IsMatch(value, "^#[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?$")) throw new ArgumentException("Use #RRGGBB or #RRGGBBAA");
        }
        // Check dimensions before Texture2D.LoadImage to reject compressed image bombs.
        public static int ValidateImage(byte[] bytes, string media)
        {
            int w = 0, h = 0;
            if (media == "image/png" && bytes.Length >= 33 && bytes[0] == 137 && bytes[1] == 80 && bytes[2] == 78 && bytes[3] == 71 &&
                bytes[4] == 13 && bytes[5] == 10 && bytes[6] == 26 && bytes[7] == 10 &&
                bytes[12] == 73 && bytes[13] == 72 && bytes[14] == 68 && bytes[15] == 82)
            {
                w = BigEndian(bytes, 16); h = BigEndian(bytes, 20);
            }
            else if (media == "image/jpeg" && bytes.Length >= 4 && bytes[0] == 255 && bytes[1] == 216)
            {
                int i = 2;
                while (i + 3 < bytes.Length)
                {
                    if (bytes[i++] != 255) break;
                    while (i < bytes.Length && bytes[i] == 255) i++;
                    if (i + 2 >= bytes.Length) break;
                    int marker = bytes[i++];
                    if (marker == 217 || marker == 218) break;
                    int len = (bytes[i] << 8) | bytes[i + 1];
                    if (len < 2 || i + len > bytes.Length) break;
                    if ((marker >= 192 && marker <= 195) && len >= 8)
                    { h = (bytes[i + 3] << 8) | bytes[i + 4]; w = (bytes[i + 5] << 8) | bytes[i + 6]; break; }
                    i += len;
                }
            }
            if (w < 1 || h < 1 || w > 2048 || h > 2048) throw new ArgumentException("Invalid image or dimensions above 2048 x 2048");
            return w * h;
        }
        static int BigEndian(byte[] b, int i) { return (b[i] << 24) | (b[i + 1] << 16) | (b[i + 2] << 8) | b[i + 3]; }
    }

    [Serializable]
    public sealed class SiteElement
    {
        public string id = "element", kind = "text", name = "Text", text = "Hello World!";
        // Position/pivot use a top-left origin: (0,0) top-left, (.5,.5) center.
        public float x = 100, y = 100, width = 240, height = 48, pivotX = .5f, pivotY = .5f;
        public string fill = "#BEBEBE", textColor = "#000000", image = "", font = "default", onClick = "";
        public int fontSize = 24;
        public List<SiteAction> actions = new List<SiteAction>();
    }
    [Serializable]
    public sealed class SiteAsset
    {
        public string name, media, data;
        public byte[] Bytes()
        {
            if (data == null || data.Length > ((SiteDocument.MaxAssetBytes + 2) / 3) * 4) throw new ArgumentException("Asset too large");
            byte[] bytes;
            try { bytes = Convert.FromBase64String(data); }
            catch (FormatException) { throw new ArgumentException("Invalid asset encoding"); }
            if (bytes.Length == 0 || bytes.Length > SiteDocument.MaxAssetBytes) throw new ArgumentException("Invalid asset size");
            return bytes;
        }
    }
}
