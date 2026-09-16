using UnityEngine;

/// <summary>
/// Внутриигровое время.
/// </summary>
/// <remarks>
/// Раньше часы в PCOS и на LED-дисплее показывали System.DateTime.Now, то есть
/// реальное время телефона. Это ломало погружение: в игре можно отыграть
/// смену, а на экране компьютера всё те же четыре утра, что и в жизни.
///
/// Теперь время считается от Main.playTime — накопленного времени игры,
/// которое уже сохраняется и загружается вместе с прогрессом (поле playtime
/// в SaveManager). Часы идут, пока игрок играет, и стоят на паузе.
///
/// Сутки в игре короче реальных: DayLengthSeconds задаёт, сколько секунд
/// реального времени занимает полный игровой день.
/// </remarks>
public static class GameClock
{
	/// <summary>
	/// Сколько реальных секунд длятся игровые сутки.
	/// </summary>
	/// <remarks>
	/// Двадцать четыре минуты: игровой час проходит за реальную минуту. Это
	/// привычный по другим играм масштаб — время заметно идёт, но не мельтешит.
	/// </remarks>
	public const float DayLengthSeconds = 24f * 60f;

	/// <summary>Час, с которого начинается новая игра.</summary>
	private const float StartHour = 9f;

	/// <summary>Сколько игровых секунд проходит за одну реальную.</summary>
	public const float SecondsPerRealSecond = 86400f / DayLengthSeconds;

	/// <summary>Игровое время в секундах от начала текущих суток.</summary>
	public static float SecondsOfDay
	{
		get
		{
			float played = 0f;
			var main = Main.Instance;
			if (main != null) played = main.playTime;

			float total = StartHour * 3600f + played * SecondsPerRealSecond;
			float wrapped = total % 86400f;
			if (wrapped < 0f) wrapped += 86400f;
			return wrapped;
		}
	}

	/// <summary>Номер игровых суток, начиная с первых.</summary>
	public static int Day
	{
		get
		{
			float played = 0f;
			var main = Main.Instance;
			if (main != null) played = main.playTime;

			float total = StartHour * 3600f + played * SecondsPerRealSecond;
			return 1 + Mathf.FloorToInt(total / 86400f);
		}
	}

	public static int Hour { get { return Mathf.FloorToInt(SecondsOfDay / 3600f) % 24; } }

	public static int Minute { get { return Mathf.FloorToInt(SecondsOfDay / 60f) % 60; } }

	public static int Second { get { return Mathf.FloorToInt(SecondsOfDay) % 60; } }

	/// <summary>
	/// Метка "сколько игровых секунд прошло" — для сравнения без сборки строки.
	/// </summary>
	/// <remarks>
	/// Часы перерисовываются, только когда эта метка изменилась. Целое число
	/// сравнивается даром, а вот склейка строки каждый кадр — это мусор в
	/// куче на каждом компьютере в мире.
	/// </remarks>
	public static int Stamp { get { return Mathf.FloorToInt(SecondsOfDay); } }
}
