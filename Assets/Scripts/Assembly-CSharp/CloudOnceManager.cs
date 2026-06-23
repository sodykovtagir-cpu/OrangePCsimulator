using CloudOnce;
using UnityEngine;

public class CloudOnceManager : MonoBehaviour
{
	public static CloudOnceManager Instance { get; private set; }

	public AchievementList Achievements;

	[SerializeField]
	private AudioClip achievementUnlocked;

	private void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
			DontDestroyOnLoad(gameObject);
		}
		else
		{
			Destroy(gameObject);
		}
	}

	private void Start()
	{
		Manager.OnAchievementUnlocked += AchievementComplete;
	}

	private void AchievementComplete(string id)
	{
		SoundManager.SPlayOneShot(achievementUnlocked);
	}

	public Achievement GetAchievementFromId(string id)
	{
		if (Achievements == null || Achievements.Entries == null)
			return null;

		foreach (var entry in Achievements.Entries)
		{
			if (entry.ID == id)
				return new Achievement(entry.ID, entry.Type);
		}

		return null;
	}
}
