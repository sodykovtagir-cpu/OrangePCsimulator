using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
	public class Virus : App
	{
		[SerializeField]
		private int backgroundIndex;

		[SerializeField]
		private Text dateText;

		[SerializeField]
		private Text countDownText;

		private DateTime target;

		private readonly string filePath;

		protected override void Start()
		{
			base.Start();
			StartCoroutine(Infect());
		}

		private IEnumerator Infect()
		{
			var os = system;
			var fm = os != null ? os.FileManager : null;

			if (fm != null && fm.TryGetFile(0, filePath, out var existing) && existing != null)
			{
				var ticks = long.Parse(existing.content);
				target = DateTime.FromBinary(ticks);
				StartCoroutine(CountDown());
				yield break;
			}

			if (transform != null) transform.localScale = Vector3.zero;
			if (os != null) os.UpdateBackground(backgroundIndex);
			yield return new WaitForSeconds(1f);
			if (transform != null) transform.localScale = Vector3.one;

			var all = os != null ? os.AllStorage : null;
			if (all != null)
			{
				for (int s = 0; s < all.Count; s++)
				{
					var storage = all[s];
					var files = storage != null ? storage.files : null;
					if (files == null) continue;
					for (int i = 0; i < files.Count; i++)
					{
						var f = files[i];
						if (f == null) continue;
						var name = f.path;
						if (name != "Launcher.exe" && name != "System/boot.bin") f.path = name + ".lck";
					}
				}
			}

			target = DateTime.Now.AddMinutes(1);
			if (fm != null)
			{
				var meta = new File(filePath, target.ToBinary().ToString(), true, 0);
				fm.Create(0, meta);
			}

	//		if (os != null) os.RefreshDesktopIcon();
			CloudOnceManager.Instance.GetAchievementFromId("oh_no")?.Unlock(null);

			StartCoroutine(CountDown());
		}

		private IEnumerator CountDown()
		{
			if (dateText != null) dateText.text = target.ToString();
			while (true)
			{
				var now = DateTime.Now;
				var span = now - target;
				if (countDownText != null) countDownText.text = span.ToString("hh\\:mm\\:ss\\.ff");
				if (now > target)
				{
					var all = system != null ? system.AllStorage : null;
					var storage = all != null && all.Count > 0 ? all[0] : null;
					if (storage != null) storage.Explode();
				}
				yield return null;
			}
		}

		public void Unlock()
		{
			if (BitcoinManager.Bitcoin < 10f) return;
			BitcoinManager.Bitcoin -= 10f;
			BitcoinManager.Save();

			var os = system;
			var fm = os != null ? os.FileManager : null;
			if (fm != null) fm.Delete(0, filePath);

			var all = os != null ? os.AllStorage : null;
			if (all != null)
			{
				for (int s = 0; s < all.Count; s++)
				{
					var storage = all[s];
					var files = storage != null ? storage.files : null;
					if (files == null) continue;
					for (int i = 0; i < files.Count; i++)
					{
						var f = files[i];
						if (f == null) continue;
						f.path = TextUtility.TrimEnd(f.path, ".lck");
					}
				}
			}

			Close();
		}
	}
}
