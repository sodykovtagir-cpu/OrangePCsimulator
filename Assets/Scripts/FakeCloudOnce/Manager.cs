using System;
namespace CloudOnce
{
    public static class Manager
    {
        public static Action<string> OnAchievementUnlocked;

        public static Action<string, float> OnIncrementalAchievementUpdated;

        internal static void AchievementUnlocked(string id)
        {
            OnAchievementUnlocked?.Invoke(id);
        }

        internal static void IncrementalAchievementUpdated(string id, float progress)
        {
            OnIncrementalAchievementUpdated?.Invoke(id, progress);
        }
    }
}
