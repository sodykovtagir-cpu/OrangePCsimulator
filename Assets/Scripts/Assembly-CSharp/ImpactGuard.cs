using UnityEngine;

/// <summary>
/// Временная защита детали от урона в момент установки в готовую сборку.
///
/// Готовый ПК ставит комплектующие прямо в слоты корпуса. В первые кадры
/// деталь ещё не поймана триггером слота, но её коллайдер уже касается
/// корпуса — физика выдаёт импульс, а обработчики урона считают его
/// поводом сломать железо:
///
/// * Breakable и Storage вызывают Hardware.Damage() по величине импульса;
/// * Destruction (и его наследник Glass — стеклянные крышки) вообще
///   уничтожает объект и подменяет его осколками.
///
/// Решение — на время установки сделать Rigidbody кинематическим:
/// кинематическое тело не участвует в расчёте импульсов, поэтому урона
/// не будет ни при каком контакте. Триггеры при этом работают штатно, так
/// что Slot.OnTriggerEnter ловит деталь как обычно.
///
/// Дополнительно на время установки отключаются сами компоненты-разрушители:
/// одной кинематики мало, если деталь толкнёт другое динамическое тело
/// (например, соседняя карта в плотно набитом майнере) — импульс в таком
/// контакте всё равно посчитается.
///
/// По истечении срока компонент возвращает всё как было и удаляет сам себя,
/// так что дальше деталь ведёт себя полностью обычно: её можно разбить,
/// сломать и разобрать.
/// </summary>
[DisallowMultipleComponent]
public class ImpactGuard : MonoBehaviour
{
	private float until;
	private Rigidbody body;
	private bool wasKinematic;
	private bool armed;

	// Компоненты, которые ломают деталь по импульсу столкновения.
	// Храним только те, что были включены — чтобы не включить лишнее.
	private Breakable breakable;
	private Destruction destruction;
	private bool breakableWasEnabled;
	private bool destructionWasEnabled;

	/// <summary>Держать защиту указанное время (в секундах).</summary>
	public void Disarm(float seconds)
	{
		until = Mathf.Max(until, Time.time + Mathf.Max(0f, seconds));

		if (armed) return;
		armed = true;

		body = GetComponent<Rigidbody>();
		if (body != null)
		{
			wasKinematic = body.isKinematic;
			body.velocity = Vector3.zero;
			body.angularVelocity = Vector3.zero;
			body.isKinematic = true;
		}

		breakable = GetComponent<Breakable>();
		if (breakable != null)
		{
			breakableWasEnabled = breakable.enabled;
			breakable.enabled = false;
		}

		// Стеклянные крышки — это Glass : Destruction. Оно не помечает
		// деталь сломанной, а уничтожает её и рассыпает осколками, поэтому
		// без этой строки аквариумные и панорамные сборки разбивались
		// прямо во время установки.
		destruction = GetComponent<Destruction>();
		if (destruction != null)
		{
			destructionWasEnabled = destruction.enabled;
			destruction.enabled = false;
		}
	}

	private void FixedUpdate()
	{
		if (Time.time < until) return;

		Release();
		Destroy(this);
	}

	private void OnDestroy()
	{
		Release();
	}

	private void Release()
	{
		if (!armed) return;
		armed = false;

		if (body != null)
		{
			// Если слот успел подключить деталь, у неё уже есть FixedJoint —
			// возврат в динамический режим ему не мешает, соединение держится.
			body.isKinematic = wasKinematic;
			body.velocity = Vector3.zero;
			body.angularVelocity = Vector3.zero;
		}

		if (breakable != null)
			breakable.enabled = breakableWasEnabled;

		if (destruction != null)
			destruction.enabled = destructionWasEnabled;
	}
}
