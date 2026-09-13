using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PC
{
	/// <summary>
	/// Спавнер готовой сборки (готовый ПК / готовый майнер из магазина).
	///
	/// Как это работает
	/// ----------------
	/// Спавнер повторяет ровно то, что делает игрок руками, но без участия
	/// физики: создаёт корпус, затем каждую деталь — и сразу подключает её
	/// в подходящий слот через Slot.TryAttach.
	///
	/// TryAttach выполняет те же проверки (тег и match) и вызывает тот же
	/// SetComponent, что и обычный OnTriggerEnter. Поэтому итог неотличим
	/// от ручной сборки: правильная поза из insertPos, FixedJoint, Connector,
	/// события слота, сохранение и возможность разобрать ПК обратно.
	///
	/// Почему не через триггеры
	/// ------------------------
	/// Прошлый вариант расставлял детали по заранее вычисленным координатам
	/// и ждал, что слот поймает их OnTriggerEnter. Это оказалось ненадёжно:
	///
	/// * OnTriggerEnter срабатывает только на ВХОД коллайдера в зону, а
	///   деталь создавалась сразу внутри триггера — события могло не быть;
	/// * пока деталь не подключена, она обычное физическое тело: падает,
	///   отскакивает и получает урон от ударов (Breakable, Storage), а
	///   стеклянные крышки (Glass : Destruction) попросту разбивались;
	/// * позы приходилось считать снаружи, и ошибка в кватернионе давала
	///   видеокарты, стоящие вверх ногами.
	///
	/// Теперь поза не вычисляется вообще: её задаёт сам слот из своего
	/// insertPos — единственный источник правды.
	///
	/// Почему не готовый префаб целиком
	/// --------------------------------
	/// Собранный ПК в принципе сериализуется (см. Assets/GameObject/Office
	/// PC.prefab — там сохранены и FixedJoint, и Connector). Но такой префаб
	/// нужно пересобирать в редакторе при каждом изменении конфигурации, а
	/// сборок девятнадцать. Спавнер даёт тот же результат, оставаясь
	/// описанием «что из чего собрать».
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

			[Tooltip("Тег слота: Motherboard, CPU, RAM, GPU, Drive, Supply...")]
			public string slotTarget;
		}

		[SerializeField]
		[Tooltip("Корпус или рама майнера — ставится первым")]
		private GameObject basePrefab;

		[SerializeField]
		private Part[] parts;

		[SerializeField]
		[Tooltip("Пауза между установками, чтобы слоты успели обновиться")]
		private float stepDelay = 0.05f;

		[SerializeField]
		[Tooltip("Уничтожить пустышку-спавнер после сборки")]
		private bool destroyAfterBuild = true;

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

			// Слоту нужен кадр, чтобы отработал его Start.
			yield return null;

			if (parts != null)
			{
				var ordered = new List<Part>(parts);
				ordered.Sort((a, b) => a.order.CompareTo(b.order));

				foreach (var part in ordered)
				{
					if (part == null || part.prefab == null) continue;

					// Деталь создаётся рядом с корпусом: точная поза не важна,
					// её всё равно задаст слот из своего insertPos.
					var spawned = Instantiate(part.prefab, root.transform.position,
						root.transform.rotation);

					var item = spawned.GetComponent<Item>();
					if (item == null)
					{
						Debug.LogWarning(
							$"{name}: у '{part.prefab.name}' нет компонента Item");
						continue;
					}

					if (!Attach(root, item, part.slotTarget))
					{
						Debug.LogWarning(
							$"{name}: не нашлось слота '{part.slotTarget}' " +
							$"для детали '{part.prefab.name}'");
						Destroy(spawned);
						continue;
					}

					// Пауза не обязательна для корректности — слоты уже
					// заняты синхронно, — но даёт материнке отработать
					// события подключения до следующей детали.
					if (stepDelay > 0f)
						yield return new WaitForSeconds(stepDelay);
				}
			}

			if (destroyAfterBuild)
				Destroy(gameObject);
		}

		/// <summary>
		/// Найти свободный подходящий слот и подключить деталь.
		///
		/// Слоты ищутся по всей текущей иерархии сборки, поэтому слоты
		/// материнской платы становятся доступны сразу после того, как сама
		/// плата встала в корпус.
		/// </summary>
		private bool Attach(GameObject root, Item item, string slotTarget)
		{
			var slots = root.GetComponentsInChildren<Slot>(true);

			foreach (var slot in slots)
			{
				if (slot == null || slot.IsUsing) continue;

				// Пустой slotTarget — «в любой подходящий»: TryAttach сам
				// проверит тег и match.
				if (!string.IsNullOrEmpty(slotTarget) && slot.target != slotTarget)
					continue;

				if (slot.TryAttach(item))
					return true;
			}

			return false;
		}
	}
}
