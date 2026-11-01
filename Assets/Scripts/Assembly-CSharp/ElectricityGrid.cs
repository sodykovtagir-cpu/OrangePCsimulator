using System;
using PC.Component;
using PC.Component.Cabling;
using UnityEngine;
using Yiming.Switch;

// Room-wide service. Added by Main in every playable room, independent of
// physical-cable mode. A future meter prefab binds to this component's public
// state and SetSwitch; the service itself is not part of the furniture save.
public sealed class ElectricityGrid : MonoBehaviour
{
    public static ElectricityGrid Instance { get; private set; }
    public static bool PowerAvailable => Instance == null || !Instance.initialized || Instance.powerAvailable;

    [SerializeField, Min(0f)] private float roomLightWatts = 100f;
    [SerializeField, Min(0f)] private float airConditionerWatts = 1000f;

    private Main main;
    private Switch lightSwitch;
    private bool initialized;
    private bool switchOn = true;
    private bool powerAvailable = true;
    private bool desiredLight;
    private bool desiredAC;
    private bool settingLight;
    private bool settingAC;
    private double unpaidDollars;
    private double totalKWh;
    private float sampleTimer;

    public bool SwitchOn => switchOn;
    public bool IsPowered => PowerAvailable;
    public bool DesiredLight => desiredLight;
    public bool DesiredAC => desiredAC;
    public float CurrentWatts { get; private set; }
    public double TotalKWh => totalKWh;
    public double PendingDollars => unpaidDollars;
    public double DollarsPerMinute => CurrentWatts / 1000d;
    public string LedText => (switchOn ? powerAvailable ? "ON" : "NO FUNDS" : "OFF") +
        "  " + CurrentWatts.ToString("0") + " W  $" +
        (main != null ? main.Money : 0).ToString("N0") +
        "  " + totalKWh.ToString("0.000") + " kWh";

    public void ConfigureLoads(float lightWatts, float acWatts)
    {
        roomLightWatts = Mathf.Max(0f, lightWatts);
        airConditionerWatts = Mathf.Max(0f, acWatts);
        if (initialized && powerAvailable) SampleWatts();
    }

    private void Awake()
    {
        Instance = this;
        main = GetComponent<Main>();
        if (main != null) main.MoneyChanged += MoneyChanged;
    }

    private void OnDestroy()
    {
        if (main != null) main.MoneyChanged -= MoneyChanged;
        if (lightSwitch != null && lightSwitch.onToggle != null)
            lightSwitch.onToggle.RemoveListener(LightToggled);
        if (Instance == this) Instance = null;
    }

    public void Initialize(GameData data, Switch roomLight)
    {
        if (data == null) return;
        if (lightSwitch != null && lightSwitch.onToggle != null)
            lightSwitch.onToggle.RemoveListener(LightToggled);
        lightSwitch = roomLight;
        switchOn = data.electricityVersion == 0 || data.electricitySwitchOn;
        desiredLight = data.electricityVersion == 0 ? data.light : data.electricityLightRequested;
        desiredAC = data.electricityVersion == 0 ? data.ac : data.electricityACRequested;
        unpaidDollars = Math.Max(0d, Math.Min(0.9999999999999999d, data.electricityUnpaid));
        totalKWh = Math.Max(0d, data.electricityKWh);
        initialized = true;
        if (lightSwitch != null && lightSwitch.onToggle != null)
            lightSwitch.onToggle.AddListener(LightToggled);
        Reevaluate();
    }

    public void SaveState(GameData data)
    {
        if (data == null || !initialized) return;
        data.electricityVersion = 1;
        data.electricitySwitchOn = switchOn;
        data.electricityLightRequested = desiredLight;
        data.electricityACRequested = desiredAC;
        data.electricityUnpaid = unpaidDollars;
        data.electricityKWh = totalKWh;
        // Legacy fields describe requested state, not a temporary blackout.
        data.light = desiredLight;
        data.ac = desiredAC;
    }

    public void SetSwitch(bool on)
    {
        switchOn = on;
        if (initialized) Reevaluate();
    }

    private void MoneyChanged()
    {
        if (initialized) Reevaluate();
    }

    // Called on appliance state changes: resample before the next billing frame.
    public void NotifyLoadChanged() { sampleTimer = 0f; }

    private void LightToggled(bool on)
    {
        if (settingLight) return;
        desiredLight = on;
        NotifyLoadChanged();
        if (!powerAvailable && on) SetActualLight(false);
    }

    private void SetActualLight(bool on)
    {
        if (lightSwitch == null || lightSwitch.IsOn == on) return;
        settingLight = true;
        try { lightSwitch.IsOn = on; }
        finally { settingLight = false; }
    }

    public void SetACRequested(bool on)
    {
        desiredAC = on;
        ApplyAC();
        NotifyLoadChanged();
    }

    private void ApplyAC()
    {
        var ac = AirConditioner.instance;
        if (ac == null || ac.Power == (powerAvailable && desiredAC)) return;
        settingAC = true;
        try { ac.Power = powerAvailable && desiredAC; }
        finally { settingAC = false; }
    }

    // AirConditioner.Power can also be changed by existing UI/scripts. Save
    // intent, but never allow actual power while the room grid is unavailable.
    public bool FilterACRequest(bool requested)
    {
        if (!initialized || settingAC) return requested;
        desiredAC = requested;
        NotifyLoadChanged();
        return requested && powerAvailable;
    }

    private void Reevaluate()
    {
        if (main == null) return;
        bool next = switchOn && main.Money > 0;
        if (powerAvailable != next)
        {
            powerAvailable = next;
            if (!next)
            {
                foreach (var board in FindObjectsOfType<Motherboard>())
                    if (board != null && board.Running) board.PowerOff(false);
            }
            foreach (var hardware in FindObjectsOfType<Hardware>())
                if (hardware != null) hardware.RefreshGridPower();
            CablePower.RefreshAll();
        }
        SetActualLight(next && desiredLight);
        ApplyAC();
        if (!next) CurrentWatts = 0f;
        else SampleWatts();
    }

    private void Update()
    {
        if (!initialized || main == null || main.IsPaused || Time.timeScale <= 0f) return;
        if (!powerAvailable) return;
        // Sampling is at 2 Hz, but energy/charges accrue every unpaused
        // frame. No per-frame FindObjectsOfType on a room full of hardware.
        sampleTimer -= Time.unscaledDeltaTime;
        if (sampleTimer <= 0f)
        {
            sampleTimer = .5f;
            SampleWatts();
        }
        double kWh = CurrentWatts * (double)Time.unscaledDeltaTime / 3600000d;
        totalKWh += kWh;
        unpaidDollars += kWh * 60d; // $1 / kW / 60 real seconds
        int due = (int)Math.Floor(unpaidDollars + 1e-9d);
        if (due <= 0) return;
        int paid = Math.Min(due, Math.Max(0, main.Money));
        unpaidDollars -= paid;
        if (paid > 0) main.SetMoney(main.Money - paid, true);
        if (unpaidDollars < 0d) unpaidDollars = 0d;
        if (paid < due || main.Money <= 0)
        {
            unpaidDollars = 0d; // no debt after the supply is disconnected
            Reevaluate();
        }
    }

    private void SampleWatts()
    {
        float watts = lightSwitch != null && lightSwitch.IsOn ? roomLightWatts : 0f;
        if (desiredAC && AirConditioner.instance != null && AirConditioner.instance.Power)
            watts += airConditionerWatts;
        foreach (var hardware in FindObjectsOfType<Hardware>())
        {
            if (hardware == null || !hardware.Power || hardware.Damaged ||
                hardware is Supply) continue; // PSU Wattage is CAPACITY
            if (hardware is CPU cpu) watts += Mathf.Max(0f, cpu.CurrentWatts);
            else if (hardware is GPU gpu) watts += Mathf.Max(0f, gpu.CurrentWatts);
            else watts += Mathf.Max(0f, hardware.Wattage);
            if (hardware is PC.Component.Motherboard board && board.Running && board.WifiEnabled)
                watts += 2f; // active PC Wi-Fi radio; never billed when the PC is off
        }
        foreach (var router in FindObjectsOfType<PC.Component.Networking.RouterDevice>())
            if (router != null && router.IsPowered)
                watts += Mathf.Max(0f, router.PowerWatts);
        CurrentWatts = watts;
    }
}
