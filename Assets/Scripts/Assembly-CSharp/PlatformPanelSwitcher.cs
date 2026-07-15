using UnityEngine;

public class PlatformPanelsSwitcher : MonoBehaviour
{
    [Header("Панель ПК-биндов")]
    [SerializeField]
    private GameObject pcPanel;

    [Header("Панель мобильной настройки")]
    [SerializeField]
    private GameObject mobilePanel;

    [Header("Для теста в Editor показывать mobile")]
    [SerializeField]
    private bool showMobileInEditor;

    private void Awake()
    {
        Apply();
    }

    private void OnEnable()
    {
        Apply();
    }

    public void Apply()
    {
        bool mobile = IsMobile();

        if (pcPanel != null)
            pcPanel.SetActive(!mobile);

        if (mobilePanel != null)
            mobilePanel.SetActive(mobile);
    }

    private bool IsMobile()
    {
#if UNITY_EDITOR
        return showMobileInEditor;
#else
        return Application.platform == RuntimePlatform.Android ||
               Application.platform == RuntimePlatform.IPhonePlayer;
#endif
    }
}