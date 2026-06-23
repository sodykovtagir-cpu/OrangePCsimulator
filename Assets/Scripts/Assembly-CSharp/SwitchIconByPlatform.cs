using UnityEngine;
using UnityEngine.UI;

public class SwitchIconByPlatform : MonoBehaviour
{
    [SerializeField]
    private Sprite android;

    [SerializeField]
    private Sprite ios;

    private void Awake()
    {
        Image x = GetComponent<Image>();
        if (x == null)
            return;

        #if UNITY_ANDROID
        x.sprite = android;
        #elif UNITY_IOS
        x.sprite = ios;
        #endif
    }
}
