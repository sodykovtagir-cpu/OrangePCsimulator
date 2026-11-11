using UnityEngine;

namespace PC.Component
{
    /// <summary>A removable caddy is not a disk. Its drive slot becomes an
    /// external of the board only while the caddy is physically latched into
    /// that board's chassis. Merely parenting it under a case is insufficient.</summary>
    public class DriveTray : Item
    {
        [SerializeField] private HardwareSlot driveSlot;
        private Motherboard.External trayExternal;
        private Case mountedCase;
        private Motherboard registeredBoard;

        public HardwareSlot DriveSlot => driveSlot;
        public bool Connected => registeredBoard != null;

        private void Awake()
        {
            trayExternal = new Motherboard.External { drive = new[] { driveSlot } };
            SlotConnected += RefreshConnection;
            SlotDisconnected += Disconnected;
        }

        private void OnEnable() { RefreshConnection(); }
        private void OnDisable() { SetCase(null); }
        private void OnDestroy()
        {
            SlotConnected -= RefreshConnection;
            SlotDisconnected -= Disconnected;
            SetCase(null);
        }

        private void Disconnected() { SetCase(null); }

        public void RefreshConnection()
        {
            var connector = GetComponent<Connector>();
            var slot = connector != null ? connector.slot : null;
            var pcCase = slot != null && slot.IsUsing && slot.AttachedItem == this && slot.target == "DriveTray"
                ? slot.GetComponentInParent<Case>() : null;
            SetCase(pcCase);
        }

        private void SetCase(Case next)
        {
            if (mountedCase != next)
            {
                if (mountedCase != null) mountedCase.BoardChanged -= RefreshBoard;
                mountedCase = next;
                if (mountedCase != null) mountedCase.BoardChanged += RefreshBoard;
            }
            RefreshBoard(mountedCase != null ? mountedCase.ActiveBoard : null);
        }

        private void RefreshBoard(Motherboard next)
        {
            if (registeredBoard == next || trayExternal == null) return;
            if (registeredBoard != null) registeredBoard.RemoveExternal(trayExternal);
            registeredBoard = next;
            if (registeredBoard != null) registeredBoard.AddExternal(trayExternal);
            else if (driveSlot != null) driveSlot.Switch(false);
        }
    }
}
