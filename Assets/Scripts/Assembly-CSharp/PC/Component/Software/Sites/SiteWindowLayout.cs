using System;
using UnityEngine;

namespace PC.Component.Software.Sites
{
    /// <summary>Fit the window inside PCOS, excluding its real taskbar inset.
    /// Do not use Screen.width/height: the computer has its own local UI scale.</summary>
    public static class SiteWindowLayout
    {
        public static float BottomInset(float height, float taskbar)
        {
            return Math.Max(0, Math.Min(taskbar, Math.Max(0, height - 1)));
        }
        public static Vector2 FittedSize(float width, float height, float taskbar)
        {
            float availableHeight = Math.Max(1, height - BottomInset(height, taskbar));
            float marginX = Math.Min(8, Math.Max(0, width) / 4);
            float marginY = Math.Min(8, availableHeight / 4);
            return new Vector2(Math.Max(1, Math.Min(900, width - 2 * marginX)),
                Math.Max(1, Math.Min(560, availableHeight - 2 * marginY)));
        }
    }
}
