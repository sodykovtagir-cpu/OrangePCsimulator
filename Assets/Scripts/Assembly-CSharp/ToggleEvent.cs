using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[RequireComponent(typeof(Toggle))]
public class ToggleEvent : MonoBehaviour
{
    public UnityEvent selected;

    protected Toggle toggle;
    protected bool oldValue;

    protected virtual void Awake()
    {
        toggle = GetComponent<Toggle>();
        oldValue = toggle.isOn;
        toggle.onValueChanged.AddListener(OnToggleClicked);
    }

    private void OnToggleClicked(bool value)
    {
        if (oldValue != value)
        {
            OnValueChanged(value);
            oldValue = value;
        }
    }

    public virtual void OnValueChanged(bool value)
    {
        if (value && selected != null)
        {
            selected.Invoke();
        }
    }
}
