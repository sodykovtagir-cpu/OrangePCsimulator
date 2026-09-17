using Newtonsoft.Json.Linq;
using UnityEngine;

namespace PC.Component
{
	/// <summary>
	/// Видеокарта. Умеет ломаться не сразу, а постепенно.
	/// </summary>
	/// <remarks>
	/// До этого видеокарта была обычным Hardware: на её префабах нет даже
	/// компонента Breakable, то есть уронить карту было нельзя вообще. Идея
	/// в том, чтобы падение не убивало карту мгновенно, а копило повреждения:
	/// сначала она начинает артефачить, и только потом умирает.
	///
	/// Это добавляет в игру состояние, которого раньше не было, — железо
	/// повреждённое, но ещё рабочее. Такую карту можно продать дешевле,
	/// поставить в майнер и терпеть полосы на экране или выбросить. Заодно
	/// появляется смысл возить комплектующие в ящиках.
	/// </remarks>
	public class GPU : Hardware
	{
		/// <summary>Сколько ступеней повреждения карта переживает до смерти.</summary>
		/// <remarks>
		/// Ноль — карта целая. На последней ступени она перестаёт работать
		/// совсем, как любое сломанное железо.
		/// </remarks>
		public const int MaxArtifactLevel = 4;

		[SerializeField]
		[Tooltip("Сила удара, начиная с которой карта получает повреждение.")]
		private float damageImpulse = 30f;

		[SerializeField]
		[Tooltip("Сколько секунд после удара карта не получает новых повреждений.")]
		private float damageCooldown = 0.5f;

		private float nextDamageTime;

		[SerializeField]
		[Tooltip("Звук повреждения карты.")]
		private AudioClip damageSound;

		private AudioSource source;

		/// <summary>
		/// Текущая степень повреждения: 0 — целая, MaxArtifactLevel — мертва.
		/// </summary>
		public int ArtifactLevel { get; private set; }

		/// <summary>Карта повреждена, но ещё выдаёт изображение.</summary>
		public bool Artifacting
		{
			get { return ArtifactLevel > 0 && ArtifactLevel < MaxArtifactLevel; }
		}

		/// <summary>
		/// Насколько сильно искажать картинку: от 0 до 1.
		/// </summary>
		/// <remarks>
		/// Отдаётся наружу, чтобы эффект на экране знал, сколько шума рисовать,
		/// и чтобы это не зависело от конкретного числа ступеней.
		/// </remarks>
		public float ArtifactStrength
		{
			get
			{
				if (ArtifactLevel <= 0) return 0f;
				return Mathf.Clamp01((float)ArtifactLevel / MaxArtifactLevel);
			}
		}

		protected override void Start()
		{
			base.Start();
			source = GetComponent<AudioSource>();
		}

		private void OnCollisionEnter(Collision collision)
		{
			if (collision == null || collision.collider == null) return;

			// Подушка в игре — это способ безопасно ронять вещи, Breakable её
			// тоже пропускает. Держимся того же правила.
			if (collision.gameObject.CompareTag("Pillow")) return;

			if (collision.impulse.magnitude <= damageImpulse) return;

			// Одно падение — одно повреждение. Упавшая карта бьётся о пол,
			// отскакивает, задевает стол и соседние предметы: это несколько
			// OnCollisionEnter подряд, и без защиты карта проходила все
			// ступени за одно падение, то есть погибала мгновенно.
			float now = Time.unscaledTime;
			if (now < nextDamageTime) return;
			nextDamageTime = now + damageCooldown;

			AddArtifactLevel(1);
		}

		/// <summary>
		/// Добавить повреждений. На последней ступени карта ломается совсем.
		/// </summary>
		public void AddArtifactLevel(int amount)
		{
			if (amount <= 0) return;
			if (Damaged) return;

			ArtifactLevel = Mathf.Min(ArtifactLevel + amount, MaxArtifactLevel);

			if (source != null && damageSound != null) source.PlayOneShot(damageSound);

			// Дошли до предела — карта больше не работает.
			if (ArtifactLevel >= MaxArtifactLevel) Damage();
		}

		public override void ToData(JObject jObject)
		{
			jObject["artifactLevel"] = ArtifactLevel;
			base.ToData(jObject);
		}

		public override void FromData(JObject jObject)
		{
			// Старые сохранения этого поля не знают: там видеокарты либо целы,
			// либо сломаны, и артефактов быть не может.
			var token = jObject["artifactLevel"];
			ArtifactLevel = token != null ? token.ToObject<int>() : 0;
			base.FromData(jObject);
		}

		public override string GetInfo()
		{
			string info = base.GetInfo();

			// Сломанная карта уже подписана базовым классом словом Broken,
			// второй раз про неё писать не нужно.
			if (!Damaged && ArtifactLevel > 0)
			{
				string text = Localization.GetText("Artifacting");
				info += "\n<color=orange>" + text + "</color>";
			}

			return info;
		}
	}
}
