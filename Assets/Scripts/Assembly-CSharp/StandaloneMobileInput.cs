using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class StandaloneMobileInput : StandaloneInputModule
{
    private MouseState mouseState = new MouseState();

    protected override MouseState GetMousePointerEventData(int id)
    {
        PointerEventData leftData = null;
        bool created = GetPointerData(-1, out leftData, true);
        leftData.Reset();

        if (created)
        {
            leftData.position = input.mousePosition;
        }

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            leftData.position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            leftData.delta = Vector2.zero;
        }
        else
        {
            Vector2 newPos = input.mousePosition;
            leftData.delta = newPos - leftData.position;
            leftData.position = newPos;
        }

        leftData.scrollDelta = input.mouseScrollDelta;
        leftData.button = PointerEventData.InputButton.Left;

        EventSystem.current.RaycastAll(leftData, m_RaycastResultCache);
        leftData.pointerCurrentRaycast = FindFirstRaycast(m_RaycastResultCache);
        m_RaycastResultCache.Clear();

        PointerEventData rightData = null;
        GetPointerData(-2, out rightData, true);
        rightData.Reset();
        CopyFromTo(leftData, rightData);
        rightData.button = PointerEventData.InputButton.Right;

        PointerEventData middleData = null;
        GetPointerData(-3, out middleData, true);
        middleData.Reset();
        CopyFromTo(leftData, middleData);
        middleData.button = PointerEventData.InputButton.Middle;

        mouseState.SetButtonState(PointerEventData.InputButton.Left,
                                 StateForMouseButton(0),
                                 leftData);
        mouseState.SetButtonState(PointerEventData.InputButton.Right,
                                 StateForMouseButton(1),
                                 rightData);
        mouseState.SetButtonState(PointerEventData.InputButton.Middle,
                                 StateForMouseButton(2),
                                 middleData);

        return mouseState;
    }
}
