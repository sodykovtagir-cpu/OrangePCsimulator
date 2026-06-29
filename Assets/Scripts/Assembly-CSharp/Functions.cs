using System.Collections.Generic;
using PC.Component;
using UnityEngine;
using UnityEngine.UI;

public class Functions : MonoBehaviour
{
	[SerializeField]
	private Image zoomImage;

	[SerializeField]
	private Sprite zoomInSprite;

	[SerializeField]
	private Sprite zoomOutSprite;

	[SerializeField]
	private Image lockRotationImage;

	[SerializeField]
	private Image removeModeImage;

	[SerializeField]
	private Image configurationImage;

	[SerializeField]
	private Image autoRotationImage;

	[SerializeField]
	private Sprite lockSprite;

	[SerializeField]
	private Sprite unlockSprite;

	[SerializeField]
	private Sprite selectSprite;

	[SerializeField]
	private Sprite selectMonitorSprite;

	[SerializeField]
	private Sprite selectCaseSprite;

	[SerializeField]
	private Sprite autoOnSprite;

	[SerializeField]
	private Sprite autoOffSprite;

	private Raycast raycast;

	private bool isZoom;

	[SerializeField]
	private Image visualWiring;

	[SerializeField]
	private Sprite visualWiringOn;

	[SerializeField]
	private Sprite visualWiringOff;

	[SerializeField]
	private ConnectLine visualLinePrefab;

	[SerializeField]
	private ParticleSystem signalParticle;

	private bool showVisualWiring;

	private List<GameObject> wires = new List<GameObject>();

	private void Start()
	{
		var player = Player.Instance;
		if (player == null) return;

		var rc = player.GetComponentInChildren<Raycast>();
		raycast = rc;
		if (rc == null) return;

		rc.ConfigurationStateChanged += Raycast_ConfigurationStateChanged;
	}

	private void Update() {}

    public void Zoom()
    {
        isZoom = !isZoom;

        var cam = Camera.main;
        if (cam == null)
            return;

        if (isZoom)
        {
            cam.fieldOfView = 20f;

            if (zoomImage != null)
            {
                zoomImage.sprite = zoomOutSprite;
                zoomImage.color = Color.red;
            }
        }
        else
        {
            float savedFov = PlayerPrefs.GetFloat("FOV", 60f);
            cam.fieldOfView = savedFov;

            if (zoomImage != null)
            {
                zoomImage.sprite = zoomInSprite;
                zoomImage.color = Color.white;
            }
        }
    }

    public void LockRotation()
	{
		var rc = raycast;
		if (rc == null) return;

		var wasLocked = rc.LockRotation;
		rc.LockRotation = !wasLocked;

		var img = lockRotationImage;
		if (img == null) return;

		if (!wasLocked)
		{
			img.sprite = lockSprite;
			var text = Localization.GetText("Lock Rotation");
			Main.Instance?.FadeText(text);
		}
		else
		{
			img.sprite = unlockSprite;
			var text = Localization.GetText("Unlock Rotation");
			Main.Instance?.FadeText(text);
		}
	}

	public void RemoveMode()
	{
		var rc = raycast;
		if (rc == null) return;

		var was = rc.RemoveMode;
		rc.RemoveMode = !was;

		var img = removeModeImage;
		if (!was)
		{
			Main.Instance?.FadeText(Localization.GetText("Click to Remove Component"));
			if (img != null) img.color = new UnityEngine.Color(1f, 0f, 0f, 1f);
		}
		else
		{
			if (img != null) img.color = UnityEngine.Color.white;
		}
	}

	public void AutoRotation()
	{
		var rc = raycast;
		if (rc == null) return;

		var was = rc.AutoRotation;
		rc.AutoRotation = !was;

		var img = autoRotationImage;
		if (img == null) return;

		if (!was)
		{
			img.sprite = autoOnSprite;
			Main.Instance?.FadeText(Localization.GetText("Auto Rotation On"));
		}
		else
		{
			img.sprite = autoOffSprite;
			Main.Instance?.FadeText(Localization.GetText("Auto Rotation Off"));
		}
	}

	public void Configuration()
	{
		var main = Main.Instance;
		var mainRc = main?.raycast;
		if (mainRc == null) return;

		var was = mainRc.Configuration;
		mainRc.Configuration = !was;
		if (was) mainRc.selectedMonitor = null;

		var rc = raycast;
		var img = configurationImage;
		if (rc == null || img == null) return;

		if (!rc.Configuration)
		{
			img.sprite = selectSprite;
			var text = "<color=red>" + Localization.GetText("Setting Failed") + "</color>";
			Main.Instance?.FadeText(text);
		}
		else
		{
			img.sprite = selectMonitorSprite;
			Main.Instance?.FadeText(Localization.GetText("Select Monitor"));
		}
	}

	private void Raycast_ConfigurationStateChanged(bool end)
	{
		var img = configurationImage;
		if (img == null) return;

		if (end)
		{
			raycast.Configuration = false;
			img.sprite = selectSprite;
			var text = "<color=lime>" + Localization.GetText("Setting Completed") + "</color>";
			Main.Instance?.FadeText(text);
		}
		else
		{
			img.sprite = selectCaseSprite;
			Main.Instance?.FadeText(Localization.GetText("Select Motherboard or Case"));
		}
	}

	public void VisualWiring()
	{
		var cam = Camera.main;
		if (cam == null) return;
		cam.depthTextureMode = DepthTextureMode.Depth;

		var was = showVisualWiring;
		showVisualWiring = !was;

		if (!was)
		{
			var mbs = FindObjectsOfType<Motherboard>();
			if (mbs != null)
			{
				foreach (var mb in mbs)
				{
					if (mb == null) continue;

					if (visualLinePrefab != null)
					{
						var line = Instantiate(visualLinePrefab);
						if (line != null)
						{
							line.a = mb.transform;
							if (mb.monitor != null) line.b = mb.monitor.transform;
							wires?.Add(line.gameObject);
						}
					}
					if (mb.WirelessRange > 0f && mb.Running)
					{
						var ps = Instantiate(signalParticle, mb.transform.position, UnityEngine.Quaternion.identity, mb.transform);
						var main = ps.main;
						main.startSize = mb.WirelessRange * 2f;
						wires?.Add(ps.gameObject);
					}
				}
			}
			if (visualWiring != null) visualWiring.sprite = visualWiringOn;
		}
		else
		{
			if (wires != null)
			{
				foreach (var go in wires) if (go) Destroy(go);
				wires.Clear();
			}
			if (visualWiring != null) visualWiring.sprite = visualWiringOff;
		}
	}
}
