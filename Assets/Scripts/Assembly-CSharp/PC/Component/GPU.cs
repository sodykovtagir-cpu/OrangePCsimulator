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

		[SerializeField]
		[Tooltip("Шанс, что повреждённая карта не даст компьютеру включиться.")]
		[Range(0f, 1f)]
		private float bootFailureChance = 0.35f;

		[SerializeField]
		[Tooltip("Шанс, что карта отвалится под нагрузкой (тест, майнинг, видео).")]
		[Range(0f, 1f)]
		private float loadFailureChance = 0.4f;

		private AudioSource source;

		/// <summary>
		/// Текущая степень повреждения: 0 — целая, MaxArtifactLevel — мертва.
		/// </summary>
		public int ArtifactLevel { get; private set; }

        // Volatile per-boot clock ratio. Hardware damage remains persisted by
        // Hardware/ArtifactLevel; merely previewing a clock does not damage it.
        public float OverclockRatio { get; set; } = 1f;
        public float Workload { get; set; }
        public float Temperature { get; private set; } = 24f;

        private void Update()
        {
            // Discrete cards have a simulated sensor; mining drives it without
            // borrowing the CPU thermometer or altering permanent hardware wear.
            float target = AirConditioner.temperature + (Power && !Damaged
                ? 12f + 54f * Mathf.Clamp01(Workload) * Mathf.Clamp(OverclockRatio, 1f, 1.5f) : 0f);
            Temperature = Mathf.Lerp(Temperature, target, Mathf.Clamp01(Time.deltaTime * .6f));
        }
        public float CurrentWatts => Power && !Damaged
            ? Wattage * (.20f + .80f * Mathf.Clamp01(Workload)) * Mathf.Clamp(OverclockRatio, 1f, 1.5f)
            : 0f;

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
                    float wear = Mathf.Clamp01((float)ArtifactLevel / MaxArtifactLevel);
                    float clock = OverclockRatio >= PC.Component.Software.OS.OverclockBoot.FatalRatio
                        ? Mathf.Clamp01(.15f + (OverclockRatio - PC.Component.Software.OS.OverclockBoot.FatalRatio) /
                            (2f - PC.Component.Software.OS.OverclockBoot.FatalRatio) * .85f)
                        : 0f;
                    return Mathf.Max(wear, clock);
			}
		}

		/// <summary>
		/// Не даст ли карта включить компьютер на этот раз.
		/// </summary>
		/// <remarks>
		/// Разыгрывается заново на КАЖДУЮ попытку запуска: в этом вся суть
		/// умирающей карты. Один раз компьютер не стартует, игрок жмёт кнопку
		/// снова — и он включается как ни в чём не бывало. Постоянный отказ
		/// читался бы как «карта сломана совсем», а для этого уже есть
		/// Damaged.
		/// </remarks>
		public bool BlocksBoot()
		{
			if (Damaged) return false;
			if (ArtifactLevel <= 0) return false;

			return Random.value < bootFailureChance * ArtifactStrength;
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

		/// <summary>
		/// Проверить карту под нагрузкой: тест, майнинг, видео.
		/// </summary>
		/// <remarks>
		/// Возвращает true, если карта не выдержала. Нагрузка — это именно то,
		/// на чём сыпется повреждённая карта: в простое она может держаться
		/// сколько угодно, а на тесте отваливается сразу.
		///
		/// Отвал НЕ означает смерть: карта добавляет ступень повреждения и
		/// роняет компьютер, но остаётся рабочей, пока ступени не кончатся.
		/// </remarks>
		public bool FailsUnderLoad()
		{
			if (Damaged) return false;
            if (ArtifactStrength <= 0f) return false;
            if (Random.value >= loadFailureChance * ArtifactStrength) return false;
            // Thermal/impact damage is persistent; an unstable clock by itself
            // causes a crash, not permanent damage until the extreme threshold.
            if (ArtifactLevel > 0) AddArtifactLevel(1);
            return true;
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
