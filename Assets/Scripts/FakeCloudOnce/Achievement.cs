using System;
using UnityEngine;

namespace CloudOnce
{
    public enum AchievementType
    {
        Incremental,
        Achievable
    }
    public class Achievement
    {
        public float achievementProgress {get; private set;}
        public string id {get; private set;}
        public AchievementType type {get; private set;}
        public bool achieved {get; private set;}
        public long achievedAt {get; private set;}
        public Achievement(string id, AchievementType type)
        {
            this.id = id;
            this.type = type;
            if (type == AchievementType.Incremental) achievementProgress = PlayerPrefs.GetFloat($"ach_{id}_progress", 0f);
            achieved = PlayerPrefs.GetInt($"ach_{id}_earned",0) == 1;            
            achievedAt = long.Parse(PlayerPrefs.GetString($"ach_{id}_earnedAt", "0"));

        }
        public void Increment(float amt, object _)
        {
            if (type != AchievementType.Incremental) throw new Exception("!Tried to increment an non-incrementable achievement!");
            achievementProgress = Math.Clamp(amt+achievementProgress,0f,100f);
            PlayerPrefs.SetFloat($"ach_{id}_progress", achievementProgress);
            Manager.IncrementalAchievementUpdated(id, achievementProgress);
            Debug.Log($"incremented {amt}, id {id}");
            if (achievementProgress >= 100f) Achieve();
        }
        public void Achieve()
        {
            if (achieved) return;
            achieved = true;
            PlayerPrefs.SetInt($"ach_{id}_earned",1);
            achievedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            PlayerPrefs.SetString($"ach_{id}_earnedAt", achievedAt.ToString());
            Manager.AchievementUnlocked(id);
            Debug.Log($"achieved {id}");
        }
        public void Unlock(object _) {Achieve();}
    }
}