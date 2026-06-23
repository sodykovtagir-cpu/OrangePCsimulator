using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public class Item : MonoBehaviour, ISave
{
	[SerializeField]
	public string spawnId;

	[SerializeField]
	private string info;

	[SerializeField]
	[Header("Slot")]
	private byte match;

	[SerializeField]
	private Vector3 slotOffset;

	public bool glue;

	public string SpawnId
	{
		get => spawnId;
		set => spawnId = value;
	}

	public string Info
	{
		get => info;
		private set => info = value;
	}

	public byte Match
	{
		get => match;
		private set => match = value;
	}

	public Vector3 SlotOffset => slotOffset;

	public int Id { get; set; }

	public event Action SlotConnected;

	public event Action SlotDisconnected;

	protected virtual void Start()
	{
		if (Id == 0) Id = Main.Instance.GetNewId(this);
	}

	public virtual string GetInfo()
	{
		if (string.IsNullOrEmpty(info))
			return info;

		string result = info;
		int start = result.IndexOf('{');

		while (start != -1)
		{
			int end = result.IndexOf('}', start);
			if (end == -1)
				break;

			string token = result.Substring(start, end - start + 1);
			string key = token.Substring(1, token.Length - 2);

			string localized = Localization.GetText(key);
			if (!string.IsNullOrEmpty(localized))
			{
				result = result.Replace(token, localized);
			}
			start = result.IndexOf('{', start + 1);
		}

		return result;
	}

	public virtual void ToData(JObject jObject)
	{
		jObject["glue"] = glue;
	}

	public virtual void FromData(JObject jObject)
	{
		jObject.TryGetValue("glue", out var val);
		glue = val.Value<bool>();
	}

	public void OnSlotConnected()
	{
		SlotConnected?.Invoke();
	}

	public void OnSlotDisconnected()
	{
		SlotDisconnected?.Invoke();
	}
}
