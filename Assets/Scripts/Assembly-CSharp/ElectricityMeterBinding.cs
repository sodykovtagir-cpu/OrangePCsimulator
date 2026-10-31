using UnityEngine;

// Attach to the future 3D switch collider (IReceiverDown/HIt) and optionally
// drag its LED TextMesh into the inspector. Nothing is spawned in existing rooms.
public sealed class ElectricityMeterBinding : MonoBehaviour, IReceiverDown
{
    [SerializeField] private TextMesh led;
    [SerializeField] private Transform lever;
    [SerializeField] private Vector3 onLocalEuler;
    [SerializeField] private Vector3 offLocalEuler = new Vector3(30f, 0f, 0f);
    private bool lastOn;
    private float nextDisplay;

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
    }
}
