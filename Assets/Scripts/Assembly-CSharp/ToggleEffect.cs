using UnityEngine;
using UnityEngine.UI;

public class ToggleEffect : ToggleEvent
{
    public Color selectedTextColor = new Color(1f, 0f, 1f, 1f); // Example default
    public Color normalTextColor = Color.white;

    private Text text;

    protected override void Awake()
    {
        base.Awake();

        var child = transform.Find("Text");
        if (child != null)
            text = child.GetComponent<Text>();
        else
            text = GetComponentInChildren<Text>();

        if (toggle != null && text != null)
        {
            text.color = toggle.isOn ? selectedTextColor : normalTextColor;
        }
    }

    public override void OnValueChanged(bool value)
    {
        if (toggle == null)
            toggle = GetComponent<Toggle>();

        if (text == null)
        {
            var c = transform.Find("Text");
            if (c != null) text = c.GetComponent<Text>();
            else text = GetComponentInChildren<Text>();
        }

        text.color = value ? selectedTextColor : normalTextColor;

        base.OnValueChanged(value); 
    }

    public void SetIsOn(bool value, bool notify = true)
    {
        if (toggle == null)
            toggle = GetComponent<Toggle>();

        if (text == null)
        {
            var c = transform.Find("Text");
            if (c != null) text = c.GetComponent<Text>();
            else text = GetComponentInChildren<Text>();
        }

        if (notify)
        {
            toggle.isOn = value;
        }
        else
        {
            toggle.SetIsOnWithoutNotify(value);
            oldValue = value;
            text.color = value ? selectedTextColor : normalTextColor;
        }
    }
}
