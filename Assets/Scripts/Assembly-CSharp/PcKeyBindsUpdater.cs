using UnityEngine;

public class PcKeybindsUpdater : MonoBehaviour
{
    private static PcKeybindsUpdater instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
#if !UNITY_ANDROID
        if (instance != null)
            return;

        GameObject go = new GameObject("ControlsSystem");
        DontDestroyOnLoad(go);

        instance = go.AddComponent<PcKeybindsUpdater>();
#endif
    }

    private void Awake()
    {
#if UNITY_ANDROID
        Destroy(gameObject);
        return;
#else
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
#endif
    }



    private void Update()
    {
#if !UNITY_ANDROID
        if (Input.GetKeyDown(KeyCode.F10))
        {
            PcKeybinds.ResetAll();
            Debug.Log("PC BINDS RESET");
        }

        PcKeybinds.TickRebind();
#endif
    }
}