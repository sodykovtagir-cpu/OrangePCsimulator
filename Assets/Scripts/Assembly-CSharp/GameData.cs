using System;

[Serializable]
public class GameData
{
	public string version;

	public string roomName;

	public int coin;

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
