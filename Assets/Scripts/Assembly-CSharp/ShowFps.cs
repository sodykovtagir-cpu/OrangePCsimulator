using UnityEngine;
using UnityEngine.UI;

public class ShowFps : MonoBehaviour
{
    private static GameObject instance;

    private void Start()
    {
        if (instance == null)
        {
            instance = GameObject.Find("Analysis");
            if (instance == null)
            {
                GameObject prefab = Resources.Load<GameObject>("Analysis");
                if (prefab != null)
                {
                    instance = Instantiate(prefab);
                    instance.name = "Analysis";
                    DontDestroyOnLoad(instance);
                    instance.SetActive(false);
                }
                else
                {
                    Debug.LogError("Prefab 'Analysis' not found in Resources folder.");
                    return;
                }
            }
        }

        Toggle toggle = GetComponent<Toggle>();
        if (toggle != null)
        {
            toggle.isOn = instance.activeSelf;
        }
    }

    public void ShowFpsDisplay(bool show)
    {
        if (instance == null)
        {
            instance = GameObject.Find("Analysis");
            if (instance == null)
            {
                Debug.LogWarning("Cannot show/hide FPS display because 'Analysis' object is missing.");
                return;
            }
        }

        instance.SetActive(show);

        Toggle toggle = GetComponent<Toggle>();
        if (toggle != null)
        {
            toggle.isOn = instance.activeSelf;
        }
    }
}
