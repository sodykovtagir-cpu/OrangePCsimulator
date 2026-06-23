using System;
using UnityEngine;
using UnityEngine.UI;

public class ResolutionSetting : MonoBehaviour
{
    [Serializable]
    private struct Container
    {
        public ToggleEvent toggle;
        public float scale;
    }

    [SerializeField] private Container[] scales;

    private static Resolution defaultResolution;

    private void Awake()
    {
        if (scales == null) return;

        float saved = PlayerPrefs.GetFloat("TargetResolution", 1f);

        for (int i = 0; i < scales.Length; i++)
        {
            var c = scales[i];
            var evt = c.toggle;
            float scale = c.scale;

            if (evt != null && evt.selected != null)
                evt.selected.AddListener(() => SetResolution(scale));

            var t = evt != null ? evt.GetComponent<Toggle>() : null;
            if (t != null && Mathf.Approximately(scale, saved))
                t.isOn = true;
        }

        RestoreSetting();
    }

    public void SetResolution(float value)
    {
        Set(value);
        PlayerPrefs.SetFloat("TargetResolution", value);
    }

    public static void RestoreSetting()
    {
        if (PlayerPrefs.HasKey("TargetResolution"))
            Set(PlayerPrefs.GetFloat("TargetResolution"));
        else
        {
            Set(1f);
            PlayerPrefs.SetFloat("TargetResolution", 1f);
        }
    }

    private static void Set(float value)
    {
        if (defaultResolution.width == 0)
            defaultResolution = Screen.currentResolution;

        int w = Mathf.RoundToInt(defaultResolution.width * value);
        int h = Mathf.RoundToInt(defaultResolution.height * value);

       Screen.SetResolution(w, h, true);
    }
}
