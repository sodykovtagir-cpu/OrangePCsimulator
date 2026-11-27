using System;

[Serializable]
public class GameData
{
    public string siteHostingWorld;
    public System.Collections.Generic.List<PC.Component.Software.Sites.SiteHostingLedger> siteHostingLedger;
    // Locally hosted sites (package lives on the player's disk) and the hosting
    // wallet bookkeeping: served seconds already reported to the backend.
    public System.Collections.Generic.List<PC.Component.Software.Sites.LocalHostedSite> siteHostingLocals;
    public PC.Component.Software.Sites.SiteHostingWallet siteHostingWallet;
    // Money moved to the hosting wallet while offline; sent on reconnect.
    public long siteHostingPendingTopUp;
    // Total money this save has moved into the hosting wallet. The server bounds
    // the wallet by it, so loading an older save takes the money back from both
    // places at once instead of leaving it duplicated on the server.
    public long siteHostingDeposited = -1;
    // Last custom domain the player used, offered again when switching modes.
    public string siteHostingCustomDomain;
	public string version;

	public string roomName;

	public int coin;
    // Version zero is a pre-meter save: default switch ON and restore light/AC intent.
    public int electricityVersion;
    public bool electricitySwitchOn;
    public bool electricityLightRequested;
    public bool electricityACRequested;
    public double electricityUnpaid;
    public double electricityKWh;

    // Shared provider account across both cable-mode save branches.
    public int internetPlan;
    public int internetGatewayId;
    public double internetPendingDollars;
    public double internetTotalMegabytes;

	public int room;

	public bool gravity;

	public bool hardcore;

	public float playtime;

	public float temperature;

	public bool ac;

	public bool light = true;

	public string sign;

	public string icon;

	public int workshopId;

	public string workshopKey;

	public int workshopSourceId;

	public string workshopTitle;

	public string workshopAuthor;

	public string workshopDesc;

    // Version 0 (missing in older saves) always means cable-required mode.
    public int cableModeVersion;
    public bool cablesEnabled = true;
    // The other mode has its own complete world snapshot in this same .opc.
    public CableModeSnapshot otherCableMode;
}

[Serializable]
public class CableModeSnapshot
{
    public string content;
    public int coin;
    public float playtime;
    public float temperature;
    public bool ac;
    public bool light;
    public string icon;
}
