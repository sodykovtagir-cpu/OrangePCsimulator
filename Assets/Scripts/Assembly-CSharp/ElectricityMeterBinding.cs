using UnityEngine;
using UnityEngine.UI;

// Attach to the future switch collider or call Hit() from a UI Button.
// TextMesh (3D), Legacy UI Text, or optional TextMeshProUGUI can display the reading.
public sealed class ElectricityMeterBinding : MonoBehaviour, IReceiverDown
{
    [SerializeField] private TextMesh led;
    [SerializeField] private Text canvasText;
    // Assign a TextMeshProUGUI component here when using TMP instead of Legacy UI Text.
    // No hard compile-time dependency on the optional TMP package.
    [SerializeField] private Component textMeshProText;
    [SerializeField] private Canvas displayCanvas;
    private System.Reflection.PropertyInfo tmpTextProperty;
    [SerializeField] private Transform lever;
    [SerializeField] private Vector3 onLocalEuler;
    [SerializeField] private Vector3 offLocalEuler = new Vector3(30f, 0f, 0f);
    private bool lastOn;
    private float nextDisplay;

    private void Awake()
    {
        if (textMeshProText == null) return;
        tmpTextProperty = textMeshProText.GetType().GetProperty("text");
        if (tmpTextProperty == null || !tmpTextProperty.CanWrite)
            Debug.LogWarning("Meter: TMP Text field needs a TextMeshProUGUI component, not a Transform.", this);
    }

    private void Start()
    {
        // A prefab cannot serialize a scene Camera reference. The World Space
        // Canvas may use the current gameplay camera for UI raycasts.
        if (displayCanvas != null && displayCanvas.renderMode == RenderMode.WorldSpace &&
            displayCanvas.worldCamera == null) displayCanvas.worldCamera = Camera.main;
    }

    public void Hit()
    {
        var grid = ElectricityGrid.Instance;
        if (grid != null) grid.SetSwitch(!grid.SwitchOn);
    }

    public void SetSwitch(bool on)
    {
        var grid = ElectricityGrid.Instance;
        if (grid != null) grid.SetSwitch(on);
    }

    private void Update()
    {
        var grid = ElectricityGrid.Instance;
        if (grid == null) return;
        if (lever != null && (lastOn != grid.SwitchOn || nextDisplay == 0f))
            lever.localRotation = Quaternion.Euler(grid.SwitchOn ? onLocalEuler : offLocalEuler);
        lastOn = grid.SwitchOn;
        if (Time.unscaledTime < nextDisplay) return;
        nextDisplay = Time.unscaledTime + .1f;
        if (led != null) led.text = grid.LedText;
        if (canvasText != null) canvasText.text = grid.LedText;
        if (textMeshProText != null && tmpTextProperty != null)
            tmpTextProperty.SetValue(textMeshProText, grid.LedText, null);
    }
}
