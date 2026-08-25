using System;
using System.Collections.Generic;
using System.IO;
using SaveManagement;
using UnityEngine;

[Serializable]
public class WorkshopLocalRec
{
	public string path;
	public int id;
	public string key;
}

[Serializable]
public class WorkshopLocalStore
{
	public WorkshopLocalRec[] items;
}

public static class WorkshopLocal
{
	private static string FilePath()
	{
		return Path.Combine(SaveUtility.GetFolderPath(), "workshop_owners.json");
	}

	private static List<WorkshopLocalRec> LoadList()
	{
		var list = new List<WorkshopLocalRec>();
		try
		{
			var p = FilePath();
			if (!File.Exists(p)) return list;
			var s = JsonUtility.FromJson<WorkshopLocalStore>(File.ReadAllText(p));
			if (s != null && s.items != null)
			{
				for (int i = 0; i < s.items.Length; i++)
					if (s.items[i] != null) list.Add(s.items[i]);
			}
		}
		catch { }
		return list;
	}

	private static void SaveList(List<WorkshopLocalRec> list)
	{
		var s = new WorkshopLocalStore { items = list.ToArray() };
		File.WriteAllText(FilePath(), JsonUtility.ToJson(s, true));
	}

	private static string Norm(string path)
	{
		if (string.IsNullOrEmpty(path)) return "";
		return Path.GetFullPath(path).Replace('\\', '/').ToLowerInvariant();
	}

	public static bool TryGet(string path, out WorkshopLocalRec rec)
	{
		rec = null;
		var n = Norm(path);
		var list = LoadList();
		for (int i = 0; i < list.Count; i++)
		{
			if (list[i] != null && Norm(list[i].path) == n && list[i].id > 0 && !string.IsNullOrEmpty(list[i].key))
			{
				rec = list[i];
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// Владелец только если в самом сейве записан workshopId и он совпадает
	/// с локальной записью. Имя файла / путь больше не делают новый сейв «тем же».
	/// </summary>
	public static bool TryGetForSave(string path, int workshopId, out WorkshopLocalRec rec)
	{
		rec = null;
		if (workshopId <= 0) return false;
		if (TryGet(path, out rec) && rec.id == workshopId) return true;
		var list = LoadList();
		for (int i = 0; i < list.Count; i++)
		{
			if (list[i] != null && list[i].id == workshopId && !string.IsNullOrEmpty(list[i].key))
			{
				rec = list[i];
				return true;
			}
		}
		rec = null;
		return false;
	}

	public static void Put(string path, int id, string key)
	{
		var n = Norm(path);
		var list = LoadList();
		for (int i = 0; i < list.Count; i++)
		{
			if (list[i] != null && Norm(list[i].path) == n)
			{
				list[i].id = id;
				list[i].key = key;
				list[i].path = path;
				SaveList(list);
				return;
			}
		}
		list.Add(new WorkshopLocalRec { path = path, id = id, key = key });
		SaveList(list);
	}

	public static void Remove(string path)
	{
		var n = Norm(path);
		var list = LoadList();
		list.RemoveAll(x => x == null || Norm(x.path) == n);
		SaveList(list);
	}
}
