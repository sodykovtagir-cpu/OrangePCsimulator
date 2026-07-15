using System.Collections.Generic;
using UnityEngine;

public class MobileHotbarOrderManager : MonoBehaviour
{
    [SerializeField]
    private Transform buttonsParent;

    [SerializeField]
    private MobileHotbarButton[] buttons;

    private const string OrderKey = "mobile_hotbar_order";

    private void Awake()
    {
        if (buttonsParent == null)
            buttonsParent = transform;

        RefreshButtons();
        ApplySavedOrder();
    }

    public void RefreshButtons()
    {
        if (buttonsParent == null)
            return;

        buttons = buttonsParent.GetComponentsInChildren<MobileHotbarButton>(true);
    }

    public List<MobileHotbarButton> GetOrderedButtons()
    {
        RefreshButtons();

        List<MobileHotbarButton> list = new List<MobileHotbarButton>();

        if (buttons != null)
            list.AddRange(buttons);

        list.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));

        return list;
    }

    public void MoveButton(string buttonId, int direction)
    {
        List<MobileHotbarButton> list = GetOrderedButtons();

        int index = -1;

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null && list[i].buttonId == buttonId)
            {
                index = i;
                break;
            }
        }

        if (index < 0)
            return;

        int newIndex = Mathf.Clamp(index + direction, 0, list.Count - 1);

        if (newIndex == index)
            return;

        MobileHotbarButton item = list[index];

        list.RemoveAt(index);
        list.Insert(newIndex, item);

        ApplyOrder(list);
        SaveOrder();
    }

    private void ApplyOrder(List<MobileHotbarButton> ordered)
    {
        for (int i = 0; i < ordered.Count; i++)
        {
            if (ordered[i] != null)
                ordered[i].transform.SetSiblingIndex(i);
        }
    }

    public void ApplySavedOrder()
    {
        RefreshButtons();

        if (!PlayerPrefs.HasKey(OrderKey))
            return;

        string saved = PlayerPrefs.GetString(OrderKey);

        if (string.IsNullOrEmpty(saved))
            return;

        string[] ids = saved.Split(',');

        List<MobileHotbarButton> all = new List<MobileHotbarButton>();

        if (buttons != null)
            all.AddRange(buttons);

        List<MobileHotbarButton> ordered = new List<MobileHotbarButton>();

        for (int i = 0; i < ids.Length; i++)
        {
            string id = ids[i];

            for (int j = 0; j < all.Count; j++)
            {
                if (all[j] != null && all[j].buttonId == id)
                {
                    ordered.Add(all[j]);
                    all.RemoveAt(j);
                    break;
                }
            }
        }

        for (int i = 0; i < all.Count; i++)
        {
            if (all[i] != null)
                ordered.Add(all[i]);
        }

        ApplyOrder(ordered);
    }

    public void SaveOrder()
    {
        List<MobileHotbarButton> list = GetOrderedButtons();

        string result = "";

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == null)
                continue;

            if (!string.IsNullOrEmpty(result))
                result += ",";

            result += list[i].buttonId;
        }

        PlayerPrefs.SetString(OrderKey, result);
        PlayerPrefs.Save();
    }

    public void ResetOrder()
    {
        PlayerPrefs.DeleteKey(OrderKey);
        PlayerPrefs.Save();
    }
}