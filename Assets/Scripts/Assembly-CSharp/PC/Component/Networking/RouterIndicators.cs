using UnityEngine;

namespace PC.Component.Networking
{
    // Binds the named meshes already authored under Router/Indicators.
    // Nothing is added to the visible prefab: only this lightweight behaviour
    // and per-instance LED materials are created at runtime.
    public sealed class RouterIndicators : MonoBehaviour
    {
        private Renderer powerLed, wanLed, wifiLed;
        private readonly Renderer[] lanLeds = new Renderer[4];
        private readonly Material[] instances = new Material[7];
        private readonly bool[] lit = new bool[7];
        private readonly float[] pulseUntil = new float[5]; // 0 Wi-Fi, 1..4 LAN
        private RouterDevice router;
        private float nextUpdate;

        private void Awake()
        {
            router = GetComponent<RouterDevice>();
            var indicators = transform.Find("Router/Indicators");
            if (indicators == null) { Debug.LogWarning("Typo-Link: Indicators not found", this); return; }
            powerLed = Find(indicators, "Power");
            wanLed = Find(indicators, "WAN");
            wifiLed = Find(indicators, "WIFI");
            for (int i = 0; i < 4; i++) lanLeds[i] = Find(indicators, "LAN" + (i + 1));
            Renderer[] all = { powerLed, wanLed, wifiLed, lanLeds[0], lanLeds[1], lanLeds[2], lanLeds[3] };
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || all[i].sharedMaterial == null) continue;
                instances[i] = new Material(all[i].sharedMaterial);
                instances[i].EnableKeyword("_EMISSION");
                all[i].material = instances[i];
                // Force a real initial OFF, not the green prefab preview.
                lit[i] = true;
                Set(i, false);
            }
        }

        private static Renderer Find(Transform parent, string name)
        {
            var child = parent.Find(name);
            return child != null ? child.GetComponent<Renderer>() : null;
        }

        public void PulseWifi() { pulseUntil[0] = Mathf.Max(pulseUntil[0], Time.unscaledTime + .28f); }
        public void PulseLan(int port)
        {
            if (port >= 1 && port <= 4)
                pulseUntil[port] = Mathf.Max(pulseUntil[port], Time.unscaledTime + .28f);
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            if (now < nextUpdate) return;
            nextUpdate = now + .08f;
            bool powered = router != null && router.IsPowered;
            var service = InternetService.Instance;
            bool internet = powered && service != null && service.Plan != 0 &&
                router.UpstreamQuality(service, new System.Collections.Generic.HashSet<int>()) > 0f;
            Set(0, powered);
            Set(1, internet);
            Set(2, powered && now < pulseUntil[0] && Mathf.FloorToInt(now * 10f) % 2 == 0);
            for (int i = 0; i < 4; i++)
                Set(i + 3, powered && now < pulseUntil[i + 1] &&
                    Mathf.FloorToInt(now * 10f) % 2 == 0);
        }

        private void Set(int index, bool on)
        {
            var mat = instances[index];
            if (mat == null || lit[index] == on) return;
            lit[index] = on;
            var active = index >= 3 ? new Color(1f, .58f, .06f) : new Color(.12f, 1f, .23f);
            var color = on ? active : new Color(.018f, .028f, .018f);
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", on ? active * 2f : Color.black);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        }

        private void OnDestroy()
        {
            for (int i = 0; i < instances.Length; i++) if (instances[i] != null) Destroy(instances[i]);
        }
    }
}
