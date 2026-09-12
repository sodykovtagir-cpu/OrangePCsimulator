using UnityEngine;

/// <summary>
/// Временная защита детали от урона в момент установки в готовую сборку.
///
/// Готовый ПК ставит комплектующие прямо в слоты корпуса. В первые кадры
/// деталь ещё не поймана триггером слота, но её коллайдер уже касается
/// корпуса — физика выдаёт импульс, а Breakable и Storage считают урон
/// именно по величине импульса (collision.impulse.magnitude) и ломают
/// новое железо прямо при рождении.
///
/// Решение — на время установки сделать Rigidbody кинематическим:
/// кинематическое тело не участвует в расчёте импульсов, поэтому урона
/// не будет ни при каком контакте. Триггеры при этом работают штатно, так
/// что Slot.OnTriggerEnter ловит деталь как обычно.
///
/// По истечении срока компонент возвращает исходный режим Rigidbody и
/// удаляет сам себя — дальше деталь ведёт себя полностью обычно.
/// </summary>
[DisallowMultipleComponent]
public class ImpactGuard : MonoBehaviour
{
	private float until;
	private Rigidbody body;
	private bool wasKinematic;
	private bool armed;

	/// <summary>Держать защиту указанное время (в секундах).</summary>
	public void Disarm(float seconds)
	{
		until = Mathf.Max(until, Time.time + Mathf.Max(0f, seconds));

		if (armed) return;

		body = GetComponent<Rigidbody>();
		if (body != null)
		{
			wasKinematic = body.isKinematic;
			body.velocity = Vector3.zero;
			body.angularVelocity = Vector3.zero;
			body.isKinematic = true;
		}
		armed = true;
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

		if (body == null) return;

		// Если слот успел подключить деталь, у неё уже есть FixedJoint —
		// возврат в динамический режим ему не мешает, соединение держится.
		body.isKinematic = wasKinematic;
		body.velocity = Vector3.zero;
		body.angularVelocity = Vector3.zero;
	}
}
