using System.Collections.Generic;
using UnityEngine;

public static class InputManager
{
    public const bool PcInput = true;

    private static Dictionary<string, float> axis;
    private static Dictionary<string, bool> virtualButtons;

    static InputManager()
    {
        axis = new Dictionary<string, float>();
        virtualButtons = new Dictionary<string, bool>();
    }

    public static void ShowCursor(bool show)
    {
        Cursor.visible = show;

#if !UNITY_ANDROID
        Cursor.lockState = show
            ? CursorLockMode.None
            : CursorLockMode.Locked;
#endif
    }

    public static void UnregisterAxis(string axisName)
    {
        if (axisName == null) return;
        axis.Remove(axisName);
    }

    public static void UpdateAxis(string axisName, float value)
    {
        if (axisName == null) return;
        axis[axisName] = value;
    }

    public static float GetAxis(string axisName)
    {
#if UNITY_ANDROID

        float mobileVal;
        return axis.TryGetValue(axisName, out mobileVal)
            ? mobileVal
            : 0f;

#else

        switch (axisName)
        {
            case "Horizontal":
                return Input.GetAxisRaw("Horizontal");

            case "Vertical":
                return Input.GetAxisRaw("Vertical");

            case "Mouse X":
                return Input.GetAxis("Mouse X");

            case "Mouse Y":
                return Input.GetAxis("Mouse Y");
        }

        float pcVal;
        return axis.TryGetValue(axisName, out pcVal)
            ? pcVal
            : 0f;

#endif
    }

    public static bool GetButton(string name)
    {
#if UNITY_ANDROID

        bool mobileVal;
        return virtualButtons.TryGetValue(name, out mobileVal)
            && mobileVal;

#else

        switch (name)
        {
            case "Run":
                return Input.GetKey(KeyCode.LeftShift);

            case "Jump":
                return Input.GetKey(KeyCode.Space);

            case "Fire":
                return Input.GetMouseButton(0);

            case "Fire2":
                return Input.GetMouseButton(1);
        }

        bool pcVal;
        return virtualButtons.TryGetValue(name, out pcVal)
            && pcVal;

#endif
    }

    public static void SetButtonDown(string name)
    {
        if (name == null) return;
        virtualButtons[name] = true;
    }

    public static void SetButtonUp(string name)
    {
        if (name == null) return;
        virtualButtons[name] = false;
    }
}