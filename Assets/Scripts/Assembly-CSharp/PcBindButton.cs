using UnityEngine;
using UnityEngine.UI;

public class PcBindButton : MonoBehaviour
{
    public PcBindAction action;
    public Text keyText;

    private void OnEnable()
    {
        Refresh();
    }

    private void Update()
    {
#if !UNITY_ANDROID
        Refresh();
#endif
    }

    public void StartRebind()
    {
#if !UNITY_ANDROID
        PcKeybinds.BeginRebind(action);
        Refresh();
#endif
    }

    public void Refresh()
    {
        if (keyText == null)
            return;

        if (PcKeybinds.IsWaitingForKey && PcKeybinds.WaitingAction == action)
        {
            keyText.text = "...";
            return;
        }

        KeyCode key = PcKeybinds.GetKey(action);
        keyText.text = PcKeybinds.GetNiceName(key);
    }
}