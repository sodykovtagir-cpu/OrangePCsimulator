using System;
using PC.Component;
using UnityEngine.Events;

public class HardwareSlot : Slot
{
	public UnityEvent onChanged;

	public Action<Hardware> HardwareConnected;
	public Action<Hardware> HardwareDisconnected;

	private bool power;

	public Hardware Hardware { get; private set; }
	public bool Ready { get; private set; }

	protected override void SetComponent(Item item)
	{
		base.SetComponent(item);
		if (item == null) return;

		var hw = item as PC.Component.Hardware;
		if (hw == null) return;
		if (!item) return;

		hw.OnDamaged += DisableComponent;

		Hardware = hw;
		Ready = true;

		if (power) hw.Switch(true, false);
		HardwareConnected?.Invoke(hw);

		onChanged?.Invoke();
	}

	protected override void RemoveComponent()
	{
		DisableComponent();
		base.RemoveComponent();
	}

	public void DisableComponent()
	{
		if (!Ready) return;

		var hw = Hardware;
		if (hw == null) return;

		hw.OnDamaged -= DisableComponent;

		Ready = false;
		hw.Switch(false, false);

		HardwareDisconnected?.Invoke(hw);

		onChanged?.Invoke();
		Hardware = null;
	}

	public void Switch(bool on)
	{
		power = on;
		if (Hardware != null) Hardware.Switch(on, false);
	}
}
