using UnityEngine;

/// <summary>
/// Вызывает действия нижней правой панели через настраиваемые ПК-бинды.
/// Старые жесткие клавиши Alpha1-Alpha8 больше не используются.
/// 
/// Работает через:
/// PcKeybinds.GetDown(PcBindAction...)
/// </summary>
public class HotbarHotkeys : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField]
    private Functions functions;

    [SerializeField]
    private MenuManager menuManager;

    [Header("Название меню магазина")]
    [SerializeField]
    private string shopMenuName = "Shop";

    [Header("Название меню заработка")]
    [SerializeField]
    private string earnMenuName = "Earn";

    private void Reset()
    {
        TryFindReferences();
    }

    private void Awake()
    {
        TryFindReferences();
    }

    private void TryFindReferences()
    {
        if (functions == null)
            functions = GetComponent<Functions>();

        if (functions == null)
            functions = GetComponentInParent<Functions>();

        if (functions == null)
            functions = FindObjectOfType<Functions>();

        if (menuManager == null)
            menuManager = GetComponent<MenuManager>();

        if (menuManager == null)
            menuManager = GetComponentInParent<MenuManager>();

        if (menuManager == null)
            menuManager = FindObjectOfType<MenuManager>();
    }

    private void Update()
    {
#if !UNITY_ANDROID
        if (PcKeybinds.IsWaitingForKey)
            return;

        if (PcKeybinds.GetDown(PcBindAction.Shop))
            OnShop();

        if (PcKeybinds.GetDown(PcBindAction.LockRotation))
            OnLockRotation();

        if (PcKeybinds.GetDown(PcBindAction.RemoveMode))
            OnRemoveMode();

        if (PcKeybinds.GetDown(PcBindAction.Zoom))
            OnZoom();

        if (PcKeybinds.GetDown(PcBindAction.Configuration))
            OnConfiguration();

        if (PcKeybinds.GetDown(PcBindAction.AutoRotation))
            OnAutoRotation();

        if (PcKeybinds.GetDown(PcBindAction.VisualWiring))
            OnVisualWiring();

        if (PcKeybinds.GetDown(PcBindAction.Earn))
            OnEarn();
#endif
    }

    public void OnShop()
    {
        if (menuManager != null)
        {
            menuManager.ShowMenu(shopMenuName);
            menuManager.PlayClickSound();
        }
        else
        {
            Debug.LogWarning("HotbarHotkeys: MenuManager не назначен, Shop не сработает.");
        }
    }

    public void OnLockRotation()
    {
        if (functions != null)
        {
            functions.LockRotation();
        }
        else
        {
            Debug.LogWarning("HotbarHotkeys: Functions не назначен, LockRotation не сработает.");
        }
    }

    public void OnRemoveMode()
    {
        if (functions != null)
        {
            functions.RemoveMode();
        }
        else
        {
            Debug.LogWarning("HotbarHotkeys: Functions не назначен, RemoveMode не сработает.");
        }
    }

    public void OnZoom()
    {
        if (functions != null)
        {
            functions.Zoom();
        }
        else
        {
            Debug.LogWarning("HotbarHotkeys: Functions не назначен, Zoom не сработает.");
        }
    }

    public void OnConfiguration()
    {
        if (functions != null)
        {
            functions.Configuration();
        }
        else
        {
            Debug.LogWarning("HotbarHotkeys: Functions не назначен, Configuration не сработает.");
        }
    }

    public void OnAutoRotation()
    {
        if (functions != null)
        {
            functions.AutoRotation();
        }
        else
        {
            Debug.LogWarning("HotbarHotkeys: Functions не назначен, AutoRotation не сработает.");
        }
    }

    public void OnVisualWiring()
    {
        if (functions != null)
        {
            functions.VisualWiring();
        }
        else
        {
            Debug.LogWarning("HotbarHotkeys: Functions не назначен, VisualWiring не сработает.");
        }
    }

    public void OnEarn()
    {
        if (menuManager != null)
        {
            menuManager.ShowMenu(earnMenuName);
            menuManager.PlayClickSound();
        }
        else
        {
            Debug.LogWarning("HotbarHotkeys: MenuManager не назначен, Earn не сработает.");
        }
    }
}