using System;
using System.Collections.Generic;
using UnityEngine;

public static class PcKeybinds
{
    private static readonly Dictionary<PcBindAction, KeyCode> keys =
        new Dictionary<PcBindAction, KeyCode>();

    private static bool loaded;

    private static bool waitingForKey;
    private static bool canAcceptKey;
    private static PcBindAction waitingAction;

    public static bool IsWaitingForKey
    {
        get { return waitingForKey; }
    }

    public static PcBindAction WaitingAction
    {
        get { return waitingAction; }
    }

    private static void EnsureLoaded()
    {
        if (loaded)
            return;

        loaded = true;

        LoadDefaults();
        LoadSaved();
    }

    private static void LoadDefaults()
    {
        keys[PcBindAction.MoveForward] = KeyCode.W;
        keys[PcBindAction.MoveBackward] = KeyCode.S;
        keys[PcBindAction.MoveLeft] = KeyCode.A;
        keys[PcBindAction.MoveRight] = KeyCode.D;

        keys[PcBindAction.Run] = KeyCode.LeftShift;
        keys[PcBindAction.Jump] = KeyCode.Space;

        keys[PcBindAction.Fire] = KeyCode.Mouse0;
        keys[PcBindAction.Fire2] = KeyCode.Mouse1;

        keys[PcBindAction.Shop] = KeyCode.Alpha1;
        keys[PcBindAction.LockRotation] = KeyCode.Alpha2;
        keys[PcBindAction.RemoveMode] = KeyCode.Alpha3;
        keys[PcBindAction.Zoom] = KeyCode.Alpha4;
        keys[PcBindAction.Configuration] = KeyCode.Alpha5;
        keys[PcBindAction.AutoRotation] = KeyCode.Alpha6;
        keys[PcBindAction.VisualWiring] = KeyCode.Alpha7;
        keys[PcBindAction.Earn] = KeyCode.E;
    }

    private static void LoadSaved()
    {
        foreach (PcBindAction action in Enum.GetValues(typeof(PcBindAction)))
        {
            string prefKey = GetPrefKey(action);

            if (!PlayerPrefs.HasKey(prefKey))
                continue;

            string saved = PlayerPrefs.GetString(prefKey);

            KeyCode key;
            if (Enum.TryParse(saved, out key))
            {
                keys[action] = key;
            }
        }
    }

    private static string GetPrefKey(PcBindAction action)
    {
        return "pc_bind_" + action;
    }

    public static KeyCode GetKey(PcBindAction action)
    {
        EnsureLoaded();

        KeyCode key;
        if (keys.TryGetValue(action, out key))
            return key;

        return KeyCode.None;
    }

    public static bool Get(PcBindAction action)
    {
        KeyCode key = GetKey(action);

        if (key == KeyCode.None)
            return false;

        return Input.GetKey(key);
    }

    public static bool GetDown(PcBindAction action)
    {
        KeyCode key = GetKey(action);

        if (key == KeyCode.None)
            return false;

        return Input.GetKeyDown(key);
    }

    public static bool GetUp(PcBindAction action)
    {
        KeyCode key = GetKey(action);

        if (key == KeyCode.None)
            return false;

        return Input.GetKeyUp(key);
    }

    public static float GetHorizontalAxis()
    {
        float value = 0f;

        if (Get(PcBindAction.MoveLeft))
            value -= 1f;

        if (Get(PcBindAction.MoveRight))
            value += 1f;

        return value;
    }

    public static float GetVerticalAxis()
    {
        float value = 0f;

        if (Get(PcBindAction.MoveBackward))
            value -= 1f;

        if (Get(PcBindAction.MoveForward))
            value += 1f;

        return value;
    }

    public static void SetKey(PcBindAction action, KeyCode key)
    {
        EnsureLoaded();

        keys[action] = key;

        PlayerPrefs.SetString(GetPrefKey(action), key.ToString());
        PlayerPrefs.Save();

        Debug.Log(action + " binded to " + key);
    }

    public static void BeginRebind(PcBindAction action)
    {
        EnsureLoaded();

        waitingAction = action;
        waitingForKey = true;
        canAcceptKey = false;

        Debug.Log("Waiting for new key: " + action);
    }

    public static void TickRebind()
    {
        if (!waitingForKey)
            return;

        /*
         * Защита от того, чтобы клик по UI-кнопке сразу
         * не назначился как Mouse0.
         */
        if (!canAcceptKey)
        {
            if (!Input.GetMouseButton(0) &&
                !Input.GetMouseButton(1) &&
                !Input.GetMouseButton(2))
            {
                canAcceptKey = true;
            }

            return;
        }

        /*
         * Escape отменяет назначение.
         * Если тебе нужно разрешить назначать Escape,
         * убери этот блок.
         */
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            waitingForKey = false;
            canAcceptKey = false;
            return;
        }

        foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
        {
            if (Input.GetKeyDown(key))
            {
                SetKey(waitingAction, key);

                waitingForKey = false;
                canAcceptKey = false;

                return;
            }
        }
    }

    public static void ResetAll()
    {
        foreach (PcBindAction action in Enum.GetValues(typeof(PcBindAction)))
        {
            PlayerPrefs.DeleteKey(GetPrefKey(action));
        }

        loaded = false;
        EnsureLoaded();

        PlayerPrefs.Save();
    }

    public static string GetNiceName(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.Mouse0:
                return "LMB";

            case KeyCode.Mouse1:
                return "RMB";

            case KeyCode.Mouse2:
                return "MMB";

            case KeyCode.LeftShift:
                return "LShift";

            case KeyCode.RightShift:
                return "RShift";

            case KeyCode.LeftControl:
                return "LCtrl";

            case KeyCode.RightControl:
                return "RCtrl";

            case KeyCode.LeftAlt:
                return "LAlt";

            case KeyCode.RightAlt:
                return "RAlt";

            case KeyCode.Space:
                return "Space";

            case KeyCode.Return:
                return "Enter";

            case KeyCode.Escape:
                return "Esc";

            case KeyCode.Alpha0:
                return "0";

            case KeyCode.Alpha1:
                return "1";

            case KeyCode.Alpha2:
                return "2";

            case KeyCode.Alpha3:
                return "3";

            case KeyCode.Alpha4:
                return "4";

            case KeyCode.Alpha5:
                return "5";

            case KeyCode.Alpha6:
                return "6";

            case KeyCode.Alpha7:
                return "7";

            case KeyCode.Alpha8:
                return "8";

            case KeyCode.Alpha9:
                return "9";

            default:
                return key.ToString();
        }
    }
}