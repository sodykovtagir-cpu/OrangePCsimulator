using UnityEngine;

public class ReflectionProbeManager : MonoBehaviour
{
    private void Awake()
    {
        RefreshAll();
    }

    public static void RefreshAll()
    {
        bool enabled = PlayerPrefs.GetInt("Reflections", 1) == 1;

        var probes = FindObjectsOfType<ReflectionProbe>(true);
        for (int i = 0; i < probes.Length; i++)
        {
            if (probes[i] != null)
                probes[i].enabled = enabled;
        }

        Debug.Log($"[ReflectionProbeManager] Reflections: {enabled} ({probes.Length} probes)");
    }
}