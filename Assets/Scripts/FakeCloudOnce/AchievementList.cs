using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

namespace CloudOnce
{
    [CreateAssetMenu(menuName = "FakeCloudOnce/Achievement List")]
    public class AchievementList : ScriptableObject
    {
        [Serializable]
        public class AchievementEntry
        {
            public string Title;
            public string Description;
            public Sprite Icon;
            public AchievementType Type;
            public string ID;
            [Description("This will make an achievement unviewable until achieved.")]
            public bool Hidden;
        }
        public AchievementEntry[] Entries;
        public AchievementsElement UI;
    }
}
