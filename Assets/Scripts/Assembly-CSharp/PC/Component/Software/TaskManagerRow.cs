using System;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
    /// <summary>Editable process-row template in TaskManager.prefab. Only copies of this
    /// template are created at runtime; fonts, masks, sizes and selection colors
    /// are configured on the template in the Unity Inspector.</summary>
    public sealed class TaskManagerRow : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Text processText;
        [SerializeField] private Text cpuText;
        [SerializeField] private Text gpuText;
        [SerializeField] private Text ramText;
        [SerializeField] private Color evenColor = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Color oddColor = new Color(.96f, .96f, .96f, 1f);
        [SerializeField] private Color selectedColor = new Color(.79f, .79f, .79f, 1f);
        private bool alternate;

        public void Bind(string name, int index, Action onClick)
        {
            if (processText != null) processText.text = name;
            alternate = index % 2 != 0;
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onClick());
            }
            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            if (background != null)
                background.color = selected ? selectedColor : alternate ? oddColor : evenColor;
        }

        public void SetMetrics(string cpu, string gpu, string ram)
        {
            if (cpuText != null) cpuText.text = cpu;
            if (gpuText != null) gpuText.text = gpu;
            if (ramText != null) ramText.text = ram;
        }
    }
}
