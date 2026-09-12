using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PC
{
	/// <summary>
	/// Спавнер готовой сборки (готовый ПК / готовый майнер из магазина).
	///
	/// Почему так, а не «префаб с уже вставленными деталями»:
	/// соединение детали со слотом живёт в рантайм-полях Slot (isUsing,
	/// attachedItem, FixedJoint) — они не сериализуются в префаб. Поэтому
	/// собранный вручную префаб выглядел бы собранным, но детали не были бы
	/// подключены: материнка не питалась бы, ПК не включался.
	///
	/// Здесь сборка воспроизводит ровно то, что делает игрок руками: корпус
	/// спавнится первым, затем каждая деталь ставится точно в insertPos своего
	/// слота. Триггер слота ловит её сам и подключает штатным путём — со всеми
	/// событиями, звуками, сохранением и возможностью разобрать ПК обратно.
	/// </summary>
	public class ReadyBuildSpawner : MonoBehaviour
	{
		[System.Serializable]
		public class Part
		{
			[Tooltip("Префаб детали из Resources/components")]
			public GameObject prefab;

			[Tooltip("Позиция относительно корпуса (insertPos слота)")]
			public Vector3 localPosition;

			[Tooltip("Поворот относительно корпуса (insertPos слота)")]
			public Vector3 localEuler;

			[Tooltip("Порядок установки: материнка раньше CPU/RAM/GPU")]
			public int order;
		}

		[SerializeField]
		[Tooltip("Корпус или рама майнера — ставится первым")]
		private GameObject basePrefab;

		[SerializeField]
		private Part[] parts;

		[SerializeField]
		[Tooltip("Пауза между установками, чтобы триггеры успели сработать")]
		private float stepDelay = 0.05f;

		[SerializeField]
		[Tooltip("Уничтожить пустышку-спавнер после сборки")]
		private bool destroyAfterBuild = true;

		[SerializeField]
		[Tooltip("Сколько ждать приземления корпуса, прежде чем ставить детали")]
		private float settleTimeout = 5f;

		private void Start()
		{
			StartCoroutine(Build());
		}

		private IEnumerator Build()
		{
			if (basePrefab == null)
			{
				Debug.LogWarning($"{name}: basePrefab не задан");
				yield break;
			}

			var root = Instantiate(basePrefab, transform.position, transform.rotation);
			var rootBody = root.GetComponent<Rigidbody>();

			// Корпус может появиться в воздухе (портал доставки, вскрытый ящик).
			// Расставлять детали сразу нельзя: связка «корпус + деталь»
			// приземляется как одно тело, удар приходится по деталям и
			// Breakable/Storage засчитывают им урон. Поэтому сперва даём
			// корпусу спокойно упасть и улечься.
			yield return StartCoroutine(WaitUntilSettled(rootBody));

			// Пока детали расставляются, корпус не должен уехать от толчков.
			bool restoreKinematic = false;
			if (rootBody != null && !rootBody.isKinematic)
			{
				rootBody.isKinematic = true;
				restoreKinematic = true;
			}

			yield return new WaitForFixedUpdate();

			if (parts != null)
			{
				var ordered = new List<Part>(parts);
				ordered.Sort((a, b) => a.order.CompareTo(b.order));

				var guards = new List<ImpactGuard>();

				for (int i = 0; i < ordered.Count; i++)
				{
					var part = ordered[i];
					if (part == null || part.prefab == null) continue;

					// Позиция слота задана в системе координат корпуса.
					var pos = root.transform.TransformPoint(part.localPosition);
					var rot = root.transform.rotation * Quaternion.Euler(part.localEuler);

					var spawned = Instantiate(part.prefab, pos, rot);

					// Деталь должна лежать неподвижно ровно в слоте, иначе
					// физика утащит её раньше, чем сработает триггер.
					var body = spawned.GetComponent<Rigidbody>();
					if (body != null)
					{
						body.velocity = Vector3.zero;
						body.angularVelocity = Vector3.zero;
					}

					// Пока деталь не поймана слотом, она не должна получать
					// урон от касания корпуса и соседних деталей: Breakable и
					// Storage ломают железо по импульсу, а Destruction/Glass
					// (стеклянные крышки) вовсе разлетаются осколками.
					var guard = spawned.AddComponent<ImpactGuard>();
					guard.Disarm(stepDelay + 0.5f);
					guards.Add(guard);

					// Защита держится до конца сборки: пока ставятся
					// оставшиеся детали, уже установленные тоже под ударом.
					float rest = (ordered.Count - i) * Mathf.Max(stepDelay, 0.02f) + 0.5f;
					for (int g = 0; g < guards.Count; g++)
					{
						if (guards[g] != null) guards[g].Disarm(rest);
					}

					// Даём физике шаг: OnTriggerEnter слота срабатывает
					// на следующем FixedUpdate после появления коллайдера.
					if (stepDelay > 0f)
						yield return new WaitForSeconds(stepDelay);
					else
						yield return new WaitForFixedUpdate();
				}
			}

			yield return new WaitForFixedUpdate();

			if (restoreKinematic && rootBody != null)
				rootBody.isKinematic = false;

			if (destroyAfterBuild)
				Destroy(gameObject);
		}

		/// <summary>
		/// Ждёт, пока корпус перестанет двигаться (или истечёт таймаут).
		/// Кинематический и уснувший Rigidbody считаются готовыми сразу.
		/// </summary>
		private IEnumerator WaitUntilSettled(Rigidbody body)
		{
			if (body == null || body.isKinematic)
			{
				yield return new WaitForFixedUpdate();
				yield break;
			}

			float deadline = Time.time + Mathf.Max(0f, settleTimeout);
			int calmFrames = 0;

			while (Time.time < deadline)
			{
				yield return new WaitForFixedUpdate();

				if (body == null) yield break;

				bool calm = body.IsSleeping()
					|| (body.velocity.sqrMagnitude < 0.01f
						&& body.angularVelocity.sqrMagnitude < 0.01f);

				calmFrames = calm ? calmFrames + 1 : 0;

				// Несколько спокойных шагов подряд — корпус точно лежит,
				// а не завис в верхней точке отскока.
				if (calmFrames >= 5) yield break;
			}
		}
	}
}
