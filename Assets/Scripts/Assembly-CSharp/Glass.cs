public class Glass : Destruction
{
	protected override void OnBreak()
	{
		var a = CloudOnceManager.Instance.GetAchievementFromId("glass_smasher");
		if (a == null) return;
		a.Increment(2f, null);
	}
}
