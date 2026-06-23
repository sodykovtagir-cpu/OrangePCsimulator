namespace PC.Component.Software
{
	public class ExplosionWebsite : Website
	{
		public void Explode()
		{
			var o = os;
			if (o == null) return;
			var all = o.AllStorage;
			if (all == null || all.Count == 0) return;
			var s = all[0] as Storage;
			if (s == null) return;
			s.Explode();
		}
	}
}
