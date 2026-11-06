using UnityEngine;
using UnityEngine.UI;

/// <summary>Scroll a clipped single-line description only while it is not edited.</summary>
public sealed class PassiveDescriptionMarquee : MonoBehaviour
{
    private Text label;
    private InputField editor;
    private string original = "";
    private string shown = "";
    private float nextStep;
    private int offset;

    public static void Attach(Text text, InputField field = null)
    {
        if (text == null) return;
        var component = text.GetComponent<PassiveDescriptionMarquee>();
        if (component == null) component = text.gameObject.AddComponent<PassiveDescriptionMarquee>();
        component.label = text;
        component.editor = field;
        component.Refresh();
    }

    public void Refresh()
    {
        if (label == null) return;
        original = editor != null ? editor.text ?? "" : label.text ?? "";
        shown = original;
        offset = 0;
        nextStep = Time.unscaledTime + 1.2f;
        label.text = original;
    }

    private void Update()
    {
        if (label == null) return;
        if (editor != null && editor.isFocused)
        {
            Restore();
            return;
        }
        string source = editor != null ? editor.text ?? "" : original;
        if (source != original)
        {
            original = source;
            Restore();
        }
        float width = editor != null ? ((RectTransform)editor.transform).rect.width - 20f
            : ((RectTransform)label.transform).rect.width;
        if (width <= 0f || original.Length == 0) return;
        // Only overflowed descriptions scroll. Keep the underlying InputField
        // value untouched, so saving/editing never stores a moving substring.
        int visible = Mathf.Max(5, Mathf.FloorToInt(width /
            Mathf.Max(1f, label.fontSize * .6f)));
        if (original.Length <= visible && label.preferredWidth <= width)
        {
            Restore();
            return;
        }
        if (Time.unscaledTime < nextStep) return;
        nextStep = Time.unscaledTime + .22f;
        string track = original + "     ";
        offset = (offset + 1) % track.Length;
        string window = track + track;
        shown = window.Substring(offset, Mathf.Min(visible, track.Length));
        label.text = shown;
    }

    private void Restore()
    {
        offset = 0;
        nextStep = Time.unscaledTime + 1.2f;
        if (label != null && label.text != original) label.text = original;
        shown = original;
    }

    private void OnDisable() { Restore(); }
}
