using System.Collections.Generic;
using UnityEngine;

public static class InputManager
{
    public const bool PcInput = true;

    private static Dictionary<string, float> axis;
    private static Dictionary<string, bool> virtualButtons;

    // Метка кадра, в котором кнопка была нажата/отпущена.
    // Один "Down" теперь виден ВСЕМ читателям в этом кадре (раньше первый
    // читатель "съедал" нажатие), а после конца кадра автоматически гаснет —
    // нажатие, сделанное в паузе, не выстрелит позже.
    private static Dictionary<string, int> virtualButtonsDown;
    private static Dictionary<string, int> virtualButtonsUp;

    static InputManager()
    {
        axis = new Dictionary<string, float>();
        virtualButtons = new Dictionary<string, bool>();

        virtualButtonsDown = new Dictionary<string, int>();
        virtualButtonsUp = new Dictionary<string, int>();
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
        if (axisName == null)
            return;

        axis.Remove(axisName);
    }

    public static void UpdateAxis(string axisName, float value)
    {
        if (axisName == null)
            return;

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
                return PcKeybinds.GetHorizontalAxis();

            case "Vertical":
                return PcKeybinds.GetVerticalAxis();

            case "Mouse X":
                return Input.GetAxis("Mouse X");

            case "Mouse Y":
                return Input.GetAxis("Mouse Y");

            case "Mouse ScrollWheel":
                return Input.GetAxis("Mouse ScrollWheel");
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
                return PcKeybinds.Get(PcBindAction.Run);

            case "Jump":
                return PcKeybinds.Get(PcBindAction.Jump);

            case "Fire":
                return PcKeybinds.Get(PcBindAction.Fire);

            case "Fire2":
                return PcKeybinds.Get(PcBindAction.Fire2);
        }

        bool pcVal;

        return virtualButtons.TryGetValue(name, out pcVal)
            && pcVal;
#endif
    }

    public static bool GetButtonDown(string name)
    {
#if UNITY_ANDROID
        return virtualButtonsDown.TryGetValue(name, out int downFrame)
            && downFrame == Time.frameCount;
#else
        switch (name)
        {
            case "Run":
                return PcKeybinds.GetDown(PcBindAction.Run);

            case "Jump":
                return PcKeybinds.GetDown(PcBindAction.Jump);

            case "Fire":
                return PcKeybinds.GetDown(PcBindAction.Fire);

            case "Fire2":
                return PcKeybinds.GetDown(PcBindAction.Fire2);
        }

        return virtualButtonsDown.TryGetValue(name, out int downFrame)
            && downFrame == Time.frameCount;
#endif
    }

    public static bool GetButtonUp(string name)
    {
#if UNITY_ANDROID
        return virtualButtonsUp.TryGetValue(name, out int upFrame)
            && upFrame == Time.frameCount;
#else
        switch (name)
        {
            case "Run":
                return PcKeybinds.GetUp(PcBindAction.Run);

            case "Jump":
                return PcKeybinds.GetUp(PcBindAction.Jump);

            case "Fire":
                return PcKeybinds.GetUp(PcBindAction.Fire);

            case "Fire2":
                return PcKeybinds.GetUp(PcBindAction.Fire2);
        }

        return virtualButtonsUp.TryGetValue(name, out int upFrame)
            && upFrame == Time.frameCount;
#endif
    }

    public static void SetButtonDown(string name)
    {
        if (name == null)
            return;

        virtualButtons[name] = true;
        virtualButtonsDown[name] = Time.frameCount;
        virtualButtonsUp.Remove(name);
    }

    public static void SetButtonUp(string name)
    {
        if (name == null)
            return;

        virtualButtons[name] = false;
        virtualButtonsUp[name] = Time.frameCount;
        virtualButtonsDown.Remove(name);
    }
}