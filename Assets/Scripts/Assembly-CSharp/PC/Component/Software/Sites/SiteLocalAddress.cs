using System;
using System.Globalization;

namespace PC.Component.Software.Sites
{
    /// <summary>Virtual PCOS disk address, never an OS file:// handler.</summary>
    public static class SiteLocalAddress
    {
        const string Prefix = "file://disk/";
        public static string Build(int diskId, string path, string asset = "")
        {
            if (diskId < 0 || !SafePath(path)) throw new ArgumentException("Invalid PCOS site path");
            string encoded = string.Join("/", Array.ConvertAll(path.Split('/'), Uri.EscapeDataString));
            if (!string.IsNullOrEmpty(asset)) asset = "#site/" + Uri.EscapeDataString(SiteCommands.RootFile(asset));
            return Prefix + diskId.ToString(CultureInfo.InvariantCulture) + "/" + encoded + asset;
        }
        public static bool TryParse(string value, out int diskId, out string path, out string asset)
        {
            diskId = -1; path = ""; asset = "";
            if (value == null || value.Length > 2048 || !value.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) return false;
            string rest = value.Substring(Prefix.Length); int slash = rest.IndexOf('/');
            if (slash < 1 || !int.TryParse(rest.Substring(0, slash), NumberStyles.None, CultureInfo.InvariantCulture, out diskId) || diskId < 0) return false;
            rest = rest.Substring(slash + 1);
            int fragment = rest.IndexOf('#');
            if (fragment >= 0)
            {
                string tail = rest.Substring(fragment);
                if (!tail.StartsWith("#site/", StringComparison.Ordinal)) return false;
                asset = Uri.UnescapeDataString(tail.Substring(6)); rest = rest.Substring(0, fragment);
                if (!SiteDocument.SafeName(asset)) return false;
            }
            if (rest.Contains("?") || rest.Contains("\\")) return false;
            var parts = rest.Split('/');
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i] = Uri.UnescapeDataString(parts[i]);
                if (parts[i].Contains("/") || parts[i].Contains("\\")) return false;
            }
            path = string.Join("/", parts); return SafePath(path);
        }
        public static bool SafePath(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Length > 1024 || path.Contains("\\") || path.Contains(":")) return false;
            var parts = path.Split('/');
            foreach (string part in parts)
            {
                string segment = part.Trim();
                if (segment.Length == 0 || segment == "." || segment == ".." || string.Equals(segment, "System", StringComparison.OrdinalIgnoreCase)) return false;
                foreach (char c in part) if (char.IsControl(c)) return false;
            }
            return path.EndsWith(".osite", StringComparison.OrdinalIgnoreCase);
        }
    }
}
