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
/// Что здесь делается, пока деталь не подключилась:
///
/// 1. отключаются сами компоненты-разрушители (Breakable, Destruction);
/// 2. деталь удерживается ровно в целевой позе, а её скорости обнуляются
///    каждый FixedUpdate — тело почти не накапливает импульс, поэтому даже
///    включённые обработчики урона не нашли бы повода сработать.
///
/// Важно: деталь НЕ делается кинематической. Кинематическое тело не создаёт
/// нормальных контактов, слот не успевает подхватить его штатным путём, а
/// FixedJoint, который Slot.SetComponent вешает на деталь, к кинематическому
/// телу просто не применяется — деталь так и остаётся висеть в воздухе рядом
/// со слотом. Именно поэтому удержание сделано «мягким», через сброс
/// скоростей, а не через isKinematic.
///
/// Как только слот принял деталь (на ней появляется Connector) или истекает
/// срок, компонент возвращает всё как было и удаляет сам себя.
/// </summary>
[DisallowMultipleComponent]
public class ImpactGuard : MonoBehaviour
{
	private float until;
	private bool armed;

	private Rigidbody body;
	private Vector3 holdPosition;
	private Quaternion holdRotation;

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
		holdPosition = transform.position;
		holdRotation = transform.rotation;

		if (body != null)
		{
			body.velocity = Vector3.zero;
			body.angularVelocity = Vector3.zero;
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
		if (!armed) return;

		// Connector слот вешает на деталь в момент подключения. Появился —
		// значит деталь уже в слоте и держать её больше не нужно.
		bool connected = GetComponent<Connector>() != null;

		if (connected || Time.time >= until)
		{
			Release();
			Destroy(this);
			return;
		}

		// Удерживаем деталь в целевой позе: без этого она за пару кадров
		// успевает соскользнуть из триггера слота и «промахивается» мимо него.
		if (body != null)
		{
			body.velocity = Vector3.zero;
			body.angularVelocity = Vector3.zero;
			body.MovePosition(holdPosition);
			body.MoveRotation(holdRotation);
		}
		else
		{
			transform.SetPositionAndRotation(holdPosition, holdRotation);
		}
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
			body.velocity = Vector3.zero;
			body.angularVelocity = Vector3.zero;
		}

		if (breakable != null)
			breakable.enabled = breakableWasEnabled;

		if (destruction != null)
			destruction.enabled = destructionWasEnabled;
	}
}
