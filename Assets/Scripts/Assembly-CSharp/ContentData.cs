using System;
using Newtonsoft.Json.Linq;

[Serializable]
public class ContentData
{
	public PlayerData playerData;

	public ItemData[] itemData;

	public JObject scene;
}
