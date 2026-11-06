using System;
using System.Collections.Generic;
using PC.Component;
using PC.Component.Cabling;
using UnityEngine;

namespace PC.Component.Networking
{
    // One provider account per room, shared by wired/wireless save branches.
    // No real HTTP traffic: prefabricated websites have simulated, editable MB.
    public sealed class InternetService : MonoBehaviour
    {
        public static InternetService Instance { get; private set; }
        public static readonly string[] PlanNames = { "Нет тарифа", "Огрызок", "Базовый минимум", "Роскошный" };
        public static readonly int[] PricePerGB = { 0, 19, 79, 499 };
        private Main main;
        private int plan;
        private int gatewayId;
        private double pendingDollars;
        private double totalMegabytes;
        private float nextGatewayScan;

        public int Plan => plan;
        public int GatewayId => gatewayId;
        public double TotalMegabytes => totalMegabytes;
        public double PendingDollars => pendingDollars;
        public string PlanName => PlanNames[Mathf.Clamp(plan, 0, 3)];
        public int RatePerGB => PricePerGB[Mathf.Clamp(plan, 0, 3)];

        private void Awake() { Instance = this; main = GetComponent<Main>(); }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void Load(GameData data)
        {
            if (data == null) return;
            plan = Mathf.Clamp(data.internetPlan, 0, 3);
            gatewayId = data.internetGatewayId;
            pendingDollars = Math.Max(0d, Math.Min(.9999999999999999d, data.internetPendingDollars));
            totalMegabytes = Math.Max(0d, data.internetTotalMegabytes);
        }

        public void Save(GameData data)
        {
            if (data == null) return;
            data.internetPlan = plan;
            data.internetGatewayId = gatewayId;
            data.internetPendingDollars = pendingDollars;
            data.internetTotalMegabytes = totalMegabytes;
        }

        // Plans are $ per consumed GB, NOT a prepaid packet or monthly fee.
        // Switching plans does not erase already incurred fractional charges.
        public bool SetPlan(int id)
        {
            if (id < 1 || id > 3) return false;
            plan = id;
            return true;
        }

        public RouterDevice Router(int id)
        {
            if (id == 0 || main == null) return null;
            var item = main.GetItemById(id);
            return item != null ? item.GetComponent<RouterDevice>() : null;
        }

        public RouterDevice Gateway()
        {
            var current = Router(gatewayId);
            if (current != null) return current;
            // A newly placed first router is the provider gateway. Lock its ID
            // once selected so a later extender cannot silently become gateway.
            if (gatewayId != 0) return null;
            // When several routers exist without a nominated provider, LEDs
            // poll Status regularly. Rate-limit this otherwise repeated scan.
            if (Time.unscaledTime < nextGatewayScan) return null;
            nextGatewayScan = Time.unscaledTime + 1f;
            RouterDevice onlyCandidate = null;
            foreach (var router in FindObjectsOfType<RouterDevice>())
            {
                if (router == null || router.Id == 0 || router.UpstreamId != 0) continue;
                // With two independent routers, the provider's side is not
                // identifiable. A PC connected to the main router can select
                // it from the local panel; never pick one by iteration order.
                if (onlyCandidate != null) return null;
                onlyCandidate = router;
            }
            if (onlyCandidate == null) return null;
            gatewayId = onlyCandidate.Id;
            // A cable may already be plugged in when the gateway becomes known.
            RouterDevice.RefreshAutoLanLinks();
            return onlyCandidate;
        }

        public bool SetGateway(int routerId)
        {
            var router = Router(routerId);
            if (router == null || router.UpstreamId != 0) return false;
            gatewayId = routerId;
            RouterDevice.RefreshAutoLanLinks();
            return true;
        }

        public NetworkStatus Status(Motherboard board)
        {
            var result = new NetworkStatus();
            if (board == null || !board.Running || !ElectricityGrid.PowerAvailable)
            { result.Message = "ПК или электросеть выключены"; return result; }

            if (CableMode.Enabled)
            {
                // A wired PC can only use a genuinely connected LAN cable.
                foreach (var port in board.GetComponentsInChildren<CablePort>(true))
                {
                    if (port.owner != board || port.kind != PortKind.PcLan || port.plug == null ||
                        port.plug.Cable == null || port.plug.Cable.Kind != CableKind.LAN) continue;
                    var other = port.plug.Cable.End(1 - port.plug.End);
                    var remote = other != null ? other.Connected : null;
                    if (remote == null || remote.kind != PortKind.RouterLan) continue;
                    var router = remote.owner != null ? remote.owner.GetComponent<RouterDevice>() : null;
                    if (router == null || !router.IsPowered) continue;
                    result.Router = router; result.Wired = true; result.Signal = 1f;
                    result.LanPort = router.PortNumber(remote);
                    break;
                }
            }
            if (result.Router == null && board.WifiEnabled && !string.IsNullOrEmpty(board.WifiSsid))
            {
                foreach (var router in FindObjectsOfType<RouterDevice>())
                {
                    if (router == null || !router.IsPowered ||
                        !router.CredentialsMatch(board.WifiSsid, board.WifiPassword)) continue;
                    float signal = router.WifiSignal(board.transform, board.WifiRange);
                    if (signal > result.Signal) { result.Router = router; result.Signal = signal; result.Wired = false; }
                }
            }
            if (result.Router == null)
            { result.Message = CableMode.Enabled ? "Нет LAN или подходящей сети Wi-Fi" :
                "Нет подходящей сети Wi-Fi"; return result; }
            result.Quality = result.Signal * result.Router.UpstreamQuality(this, new HashSet<int>());
            result.Message = result.Quality > 0f ? "Подключено" : "Роутер без подключения к провайдеру";
            return result;
        }

        public bool JoinWifi(Motherboard board, string ssid, string password)
        {
            if (board == null || !board.Running || !board.WifiEnabled ||
                string.IsNullOrWhiteSpace(ssid) || !ElectricityGrid.PowerAvailable) return false;
            foreach (var router in FindObjectsOfType<RouterDevice>())
                if (router != null && router.IsPowered && router.CredentialsMatch(ssid, password) &&
                    router.WifiSignal(board.transform, board.WifiRange) > 0f)
                { board.ConfigureWifi(ssid, password); return true; }
            return false;
        }

        // LAN connections require a physical cable. Old saved virtual LAN IDs
        // are ignored by Status in cable-free mode.
        public bool JoinLan(Motherboard board, int routerId)
        {
            if (!CableMode.Enabled || board == null) return false;
            var router = Router(routerId);
            var status = Status(board);
            return router != null && status.Router == router && status.Wired;
        }

        public bool CanOpenLocalPanel(Motherboard board, out RouterDevice router)
        {
            var link = Status(board);
            router = link.Router;
            return router != null && router.IsPowered; // tariff not required for LAN administration
        }

        public bool TryTransfer(Motherboard board, string url, float megabytes,
            out float delay, out string error)
        {
            delay = 0f; error = null;
            var status = Status(board);
            if (status.Router == null || status.Quality <= 0f)
            { error = status.Message; return false; }
            if (plan == 0) { error = "Тариф не подключён. Открой www.typolink.com/tariffs"; return false; }
            if (main == null || main.Money <= 0)
            { error = "Недостаточно денег для интернета"; return false; }

            // Some sites on the worst plan consistently never load. The other
            // loss is transient and changes on retry, and both increase with
            // poor signal / unstable repeaters.
            float failure = plan == 1 ? .20f : plan == 2 ? .055f : 0f;
            failure += (1f - status.Quality) * (plan == 1 ? .32f : plan == 2 ? .11f : .03f);
            if (plan == 1 && StableHash(url) % 4u == 1u)
            { error = "Сайт недоступен на тарифе «Огрызок»"; return false; }
            if (UnityEngine.Random.value < failure)
            { error = "Соединение нестабильно — попробуй ещё раз"; return false; }

            double mb = Math.Max(0d, megabytes);
            double cost = pendingDollars + mb * RatePerGB / 1024d;
            int due = (int)Math.Floor(cost + 1e-9d);
            // Keep the last dollar for electricity: otherwise billing turns
            // the computer off halfway through the page load.
            if (due >= main.Money)
            { error = "Нужны деньги на трафик и хотя бы $1 на питание ПК"; return false; }
            SignalTraffic(board);
            pendingDollars = Math.Max(0d, cost - due);
            totalMegabytes += mb;
            if (due > 0) main.SetMoney(main.Money - due, true);

            float mbPerSecond = plan == 1 ? .45f : plan == 2 ? 5f : 75f;
            float overhead = plan == 1 ? 2.2f : plan == 2 ? .7f : .05f;
            delay = Mathf.Clamp(overhead + (float)mb / (mbPerSecond * Mathf.Max(.10f, status.Quality)),
                .05f, 60f);
            return true;
        }

        // Blink only on actual requests, never just because a PC is powered.
        public void SignalTraffic(Motherboard board)
        {
            var link = Status(board);
            if (link.Router == null || link.Quality <= 0f) return;
            var router = link.Router;
            if (link.Wired) router.Indicators?.PulseLan(link.LanPort);
            else router.Indicators?.PulseWifi();
            var visited = new HashSet<int>();
            while (router != null && router.UpstreamId != 0 && visited.Add(router.Id))
            {
                var parent = Router(router.UpstreamId);
                if (parent == null) break;
                if (router.UpstreamWireless)
                {
                    router.Indicators?.PulseWifi();
                    parent.Indicators?.PulseWifi();
                }
                else
                {
                    int localPort = router.LocalUpstreamLanPort(parent);
                    int parentPort = router.UpstreamLanPortIndex;
                    // A replugged cable can use a different socket than the
                    // last saved/configured one; blink the live physical port.
                    if (CableMode.Enabled &&
                        !router.PhysicalLanTo(parent, out localPort, out parentPort)) break;
                    router.Indicators?.PulseLan(localPort);
                    parent.Indicators?.PulseLan(parentPort);
                }
                router = parent;
            }
        }

        private static uint StableHash(string text)
        {
            uint h = 2166136261u;
            if (text != null) foreach (char c in text.ToLowerInvariant()) h = unchecked((h ^ c) * 16777619u);
            return h;
        }
    }

    public sealed class NetworkStatus
    {
        public RouterDevice Router;
        public bool Wired;
        public int LanPort;
        public float Signal;
        public float Quality;
        public string Message;
    }
}
