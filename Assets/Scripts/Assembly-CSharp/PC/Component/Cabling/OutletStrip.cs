using UnityEngine;

namespace PC.Component.Cabling
{
    [RequireComponent(typeof(Hook))]
    public sealed class OutletStrip : MonoBehaviour
    {
        [Range(1, 3)] public int sockets = 2;
        private Hook hook;
        public bool IsMounted => isActiveAndEnabled && hook != null && hook.Hooked;

        private void Awake()
        {
            hook = GetComponent<Hook>();
            // Leave the designer's plate/model intact when one is in the prefab.
            if (GetComponentInChildren<Renderer>() == null)
            {
                var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plate.name = "Replace with outlet model";
                plate.transform.SetParent(transform, false);
                plate.transform.localScale = new Vector3(.55f, .28f, .12f);
                Destroy(plate.GetComponent<Collider>());
            }
            var item = GetComponent<Item>();
            for (int i = 0; i < sockets; i++)
                CablePort.Add(item, PortKind.Outlet, "Outlet " + (i + 1),
                    new Vector3((i - (sockets - 1) * .5f) * .16f, 0f, .07f), .28f);
        }

        private void OnDisable()
        {
            CablePower.RefreshAll();
        }
    }
}
