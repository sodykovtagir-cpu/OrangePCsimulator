using System;
using System.Collections.Generic;
using CloudOnce;
using UnityEngine;
using static CloudOnce.AchievementList;

public class AchievementsUI : MonoBehaviour
{
    [SerializeField]
    private Sprite unknown;

    [SerializeField]
    private RectTransform canvas;

    void OnEnable()
    {
        Manager.OnAchievementUnlocked += OnAchievementChanged;
        Manager.OnIncrementalAchievementUpdated += OnIncrementalChanged;
        Refresh();
    }

    void OnDisable()
    {
        Manager.OnAchievementUnlocked -= OnAchievementChanged;
        Manager.OnIncrementalAchievementUpdated -= OnIncrementalChanged;
    }

    void OnAchievementChanged(string id)
    {
        Refresh();
    }

    void OnIncrementalChanged(string id, float value)
    {
        Refresh();
    }

    void Refresh()
    {
        for (int i = canvas.transform.childCount - 1; i >= 0; i--)
        {
            Destroy(canvas.transform.GetChild(i).gameObject);
        }
        foreach (AchievementEntry l in CloudOnceManager.Instance.Achievements.Entries)
        {
            var i = Instantiate(CloudOnceManager.Instance.Achievements.UI, canvas);
            Achievement achievement = CloudOnceManager.Instance.GetAchievementFromId(l.ID);
            if (!l.Hidden || achievement.achieved) {
                i.desc.text = l.Description;
                i.title.text = l.Title;
                i.image.texture = l.Icon.texture;
                i.locked.enabled = !achievement.achieved;

                string inf = "Never unlocked";

                if (achievement.type == AchievementType.Incremental)
                {
                    i.progressable.gameObject.SetActive(true);
                    inf += $" [{achievement.achievementProgress}%]";
                    float progress = Mathf.Clamp(achievement.achievementProgress, 0f, 100f) / 100f;
                    i.progressBar.localScale = new Vector3(progress, 1f, 1f);
                }
                else
                {
                    i.progressable.gameObject.SetActive(false);
                }
                i.info.text = achievement.achieved ? $"Unlocked at {DateTimeOffset.FromUnixTimeSeconds(achievement.achievedAt).ToLocalTime():dd MMM yyyy, HH:mm}" : inf;
            } else
            {
                i.desc.text = "???";
                i.title.text = "Hidden";
                i.image.texture = unknown.texture;
                i.info.text = "";
            }
        }
    }
}
