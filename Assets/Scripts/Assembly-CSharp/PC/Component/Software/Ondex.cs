using UnityEngine;

namespace PC.Component.Software
{
	/// <summary>
	/// Ondex Browser — браузер, который тащит вирусы почти с каждого сайта.
	/// </summary>
	/// <remarks>
	/// Идея из чата канала: «добавь браузер, где все сайты ставят вирус, когда
	/// на них заходишь». Это отдельное приложение, а не режим обычного
	/// браузера: игрок ставит его сам и сам виноват.
	///
	/// Работает как обычный Browser, но на каждом переходе бросает кубик. Шанс
	/// намеренно высокий — в этом вся шутка, — однако не стопроцентный: если
	/// вирус выпадает всегда, приложением просто никто не пользуется, а так
	/// остаётся азарт.
	/// </remarks>
	public class Ondex : Browser
	{
		[Header("Ondex")]
		[SerializeField]
		[Tooltip("Вероятность поймать вирус при переходе на сайт, от 0 до 1.")]
		[Range(0f, 1f)]
		private float infectionChance = 0.65f;

		[SerializeField]
		[Tooltip("Префаб вируса, который ставится на систему.")]
		private Virus virusPrefab;

		[SerializeField]
		[Tooltip("Звук заражения.")]
		private AudioClip infectionSound;

		/// <summary>Уже заражённая система повторно не заражается.</summary>
		private bool infected;

		protected override void OpenSite(WebsiteItem site)
		{
			base.OpenSite(site);

			TryInfect();
		}

		private void TryInfect()
		{
			if (infected) return;
			if (virusPrefab == null) return;

			var os = system;
			if (os == null) return;

			if (Random.value > infectionChance) return;

			infected = true;

			var source = GetComponent<AudioSource>();
			if (source != null && infectionSound != null) source.PlayOneShot(infectionSound);

			// Вирус живёт как обычное приложение системы: он сам зашифрует
			// файлы, заведёт таймер и выдаст достижение.
			var instance = Instantiate(virusPrefab, transform.parent);
			instance.Init(os);
		}
	}
}
