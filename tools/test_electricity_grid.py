#!/usr/bin/env python3
"""Source-level safeguards for the meter; real Unity play tests still required."""
from pathlib import Path
from math import floor

src = Path(__file__).resolve().parents[1] / 'Assets/Scripts/Assembly-CSharp'
def read(path): return (src / path).read_text()
grid = read('ElectricityGrid.cs')
binding = read('ElectricityMeterBinding.cs')
main = read('Main.cs')
game = read('GameData.cs')
saver = read('SaveManager.cs')
power = read('PC/Component/Cabling/CablePower.cs')
hardware = read('PC/Component/Hardware.cs')
ac = read('AirConditioner.cs')
mode = read('PC/Component/Cabling/CableMode.cs')
assert 'gameObject.AddComponent<ElectricityGrid>()' in main
assert 'grid.ConfigureLoads(roomLightWatts, airConditionerWatts)' in main
assert 'private float roomLightWatts = 100f;' in main
assert 'private float airConditionerWatts = 1000f;' in main
assert 'public bool IsPaused => paused;' in main
assert 'Time.unscaledDeltaTime' in grid and 'main.IsPaused || Time.timeScale <= 0f' in grid
assert 'CurrentWatts * (double)Time.unscaledDeltaTime / 3600000d' in grid
assert 'unpaidDollars += kWh * 60d;' in grid
assert 'Math.Floor(unpaidDollars' in grid
assert 'Math.Min(due, Math.Max(0, main.Money))' in grid
assert 'main.SetMoney(main.Money - paid, true)' in grid
assert 'data.electricityVersion == 0 || data.electricitySwitchOn' in grid
assert all(f'public {typ} {name};' in game.split('public class CableModeSnapshot')[0]
           for typ, name in (('int','electricityVersion'), ('bool','electricitySwitchOn'),
                             ('bool','electricityLightRequested'), ('bool','electricityACRequested'),
                             ('double','electricityUnpaid'), ('double','electricityKWh')))
assert saver.index('Instance.Initialize(Loader.GameData, lamp)') < saver.index('if (string.IsNullOrEmpty(Loader.Content))')
assert 'ElectricityGrid.Instance.SaveState(game)' in saver
assert 'data.light = desiredLight;' in grid and 'data.ac = desiredAC;' in grid
assert 'game.electricityLightRequested' in mode and 'game.electricityACRequested' in mode
assert 'main.Money > 0' in grid and 'if (board != null && board.Running) board.PowerOff(false)' in grid
assert 'hardware.RefreshGridPower()' in grid
assert 'ElectricityGrid.PowerAvailable' in power and 'ElectricityGrid.PowerAvailable' in hardware
assert 'if (!CableMode.Enabled) return true;' in power
assert 'if (!CableMode.Enabled) return;' in power
assert 'grid.FilterACRequest(value)' in ac and 'grid.SetACRequested(!grid.DesiredAC)' in ac
assert 'hardware is Supply) continue' in grid
assert 'cpu.CurrentWatts' in grid and 'gpu.CurrentWatts' in grid
assert 'lightSwitch != null && lightSwitch.IsOn' in grid
assert 'class ElectricityMeterBinding : MonoBehaviour, IReceiverDown' in binding
assert 'grid.SetSwitch(!grid.SwitchOn)' in binding and 'led.text = grid.LedText' in binding
assert 'canvasText.text = grid.LedText' in binding and 'displayCanvas.worldCamera = Camera.main;' in binding
assert 'tmpTextProperty.SetValue(textMeshProText, grid.LedText, null)' in binding
# Independent arithmetic smoke examples for rate/whole dollar and save residual.
def charge(watts, seconds, initial=0):
    accrued = initial + watts / 1000 * seconds / 60
    return floor(accrued + 1e-9), accrued - floor(accrued + 1e-9)
assert charge(1000, 59)[0] == 0
assert charge(1000, 1, charge(1000, 59)[1])[0] == 1
assert charge(2000, 150)[0] == 5
assert charge(100, 600)[0] == 1
print('PASS: meter rate, fractional save, old saves, modes, power guards, model interface')
