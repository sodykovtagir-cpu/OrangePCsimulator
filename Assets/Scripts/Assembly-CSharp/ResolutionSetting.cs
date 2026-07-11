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

    [Header("Resolution")]
    [SerializeField] private Container[] scales;

    [Header("Graphics Toggles")]
    [SerializeField] private Toggle rtxToggle;
    [SerializeField] private Toggle reflectionsToggle;

    private void Awake()
    {
        // === Разрешение ===
        if (scales != null)
        {
            float saved = PlayerPrefs.GetFloat("TargetResolution", 1f);

            for (int i = 0; i < scales.Length; i++)
            {
                var c = scales[i];
                var evt = c.toggle;
                float scale = c.scale;

                var t = evt != null ? evt.GetComponent<Toggle>() : null;

                if (t != null)
                {
                    if (Mathf.Approximately(scale, saved))
                        t.SetIsOnWithoutNotify(true);

                    t.onValueChanged.AddListener(isOn =>
                    {
                        if (isOn) SetResolution(scale);
                    });
                }

                if (evt != null && evt.selected != null)
                    evt.selected.AddListener(() => SetResolution(scale));
            }
        }

        // === RTX ===
        if (rtxToggle != null)
        {
            bool savedRTX = PlayerPrefs.GetInt("RTXMode", 0) == 1;
            rtxToggle.SetIsOnWithoutNotify(savedRTX);
            rtxToggle.onValueChanged.AddListener(SetRTXMode);
        }

        // === Отражения ===
        if (reflectionsToggle != null)
        {
            bool savedRefl = PlayerPrefs.GetInt("Reflections", 1) == 1;
            reflectionsToggle.SetIsOnWithoutNotify(savedRefl);
            reflectionsToggle.onValueChanged.AddListener(SetReflections);
        }
    }

    public void SetResolution(float value)
    {
        PlayerPrefs.SetFloat("TargetResolution", value);
        PlayerPrefs.Save();
        GraphicsBootstrap.ApplyResolution();
        Debug.Log($"[Res] SetResolution: {value}");
    }

    private void SetRTXMode(bool enabled)
    {
        PlayerPrefs.SetInt("RTXMode", enabled ? 1 : 0);
        PlayerPrefs.Save();
        GraphicsBootstrap.ApplyRTX();

        if (enabled)
        {
            PlayerPrefs.SetFloat("TargetResolution", 2f);
            PlayerPrefs.Save();
            GraphicsBootstrap.ApplyResolution();
        }
    }

    private void SetReflections(bool enabled)
    {
        PlayerPrefs.SetInt("Reflections", enabled ? 1 : 0);
        PlayerPrefs.Save();

        GraphicsBootstrap.ApplyReflectionsToScene();
    }
}