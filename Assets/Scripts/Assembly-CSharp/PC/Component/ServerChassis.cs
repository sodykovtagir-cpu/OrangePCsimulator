using UnityEngine;

namespace PC.Component
{
    /// <summary>Latch the artist-authored lid and four caddies on a newly
    /// purchased chassis. Saves restore those independent items themselves;
    /// do not spawn another default set when loading the case prefab.</summary>
    public class ServerChassis : MonoBehaviour
    {
        [SerializeField] private Item factoryCover;
        [SerializeField] private Slot coverSlot;
        [SerializeField] private DriveTray[] factoryTrays;
        [SerializeField] private Slot[] trayBays;
        [SerializeField] private Item factoryDrive;
        [SerializeField] private HardwareSlot factoryDriveSlot;
        [System.Serializable]
        public class FactoryPart
        {
            public Item item;
            public Slot slot;
        }

        // All geometry is serialized in the ready-server prefab. This list
        // only restores slot state/joints on the already-present components.
        [SerializeField] private FactoryPart[] factoryParts;
        private bool loading;

        private void Awake()
        {
            loading = SaveManager.RestoringSave;
            if (!loading) return;
            if (factoryParts != null)
                for (int i = factoryParts.Length - 1; i >= 0; i--)
                    if (factoryParts[i] != null) RemoveFactoryPart(factoryParts[i].item);
            RemoveFactoryPart(factoryDrive);
            RemoveFactoryPart(factoryCover);
            if (factoryTrays != null)
                foreach (var tray in factoryTrays) RemoveFactoryPart(tray);
        }

        private static void RemoveFactoryPart(Item part)
        {
            if (part == null) return;
            part.gameObject.SetActive(false);
            part.transform.SetParent(null, true);
            Destroy(part.gameObject);
        }

        private void Start()
        {
            if (loading) return;
            if (factoryCover != null && coverSlot != null) coverSlot.TryAttach(factoryCover);
            if (factoryTrays != null && trayBays != null)
                for (int i = 0; i < Mathf.Min(factoryTrays.Length, trayBays.Length); i++)
                    if (factoryTrays[i] != null && trayBays[i] != null)
                        trayBays[i].TryAttach(factoryTrays[i]);
            if (factoryParts != null)
                foreach (var part in factoryParts)
                    if (part != null && part.item != null && part.slot != null && !part.slot.IsUsing)
                        part.slot.TryAttach(part.item);
            if (factoryDrive != null && factoryDriveSlot != null)
                factoryDriveSlot.TryAttach(factoryDrive);
        }
    }
}
