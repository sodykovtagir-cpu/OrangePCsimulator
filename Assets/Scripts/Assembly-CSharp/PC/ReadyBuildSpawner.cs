using System.Collections;
using System.Collections.Generic;
using PC.Component;
using PC.Component.Software;
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
		[Tooltip("Предустановить PCOS на первый накопитель сборки")]
		private bool preinstallOS = true;

		[SerializeField]
		[Tooltip("Приложения, которые лежат на диске вместе с системой. " +
			"Имена — как AppName в префабах из Resources/apps")]
		private string[] preinstalledApps;

		[SerializeField]
		[Tooltip("Размер загрузчика PCOS (Installer.minimumSpace)")]
		private int systemSize = 60000;

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

			// Хосты слотов. Корпус — первый, дальше сюда попадает каждая
			// подключённая деталь: у слота Motherboard во всех корпусах и
			// рамах стоит setParent: 0, поэтому плата НЕ становится потомком
			// корпуса (её держит только FixedJoint) и её слоты недостижимы
			// через GetComponentsInChildren от корпуса.
			var hosts = new List<GameObject> { root };

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

					if (!Attach(hosts, item, part.slotTarget))
					{
						Debug.LogWarning(
							$"{name}: не нашлось слота '{part.slotTarget}' " +
							$"для детали '{part.prefab.name}'");
						Destroy(spawned);
						continue;
					}

					// Деталь могла сама принести слоты (материнская плата),
					// и достижимы они только через неё.
					hosts.Add(spawned);

					// Пауза не обязательна для корректности — слоты уже
					// заняты синхронно, — но даёт материнке отработать
					// события подключения до следующей детали.
					if (stepDelay > 0f)
						yield return new WaitForSeconds(stepDelay);
				}
			}

			if (preinstallOS)
				InstallSystem(hosts);

			if (destroyAfterBuild)
				Destroy(gameObject);
		}

		/// <summary>
		/// Записать PCOS и набор приложений на первый накопитель сборки.
		///
		/// Система — это просто файлы: Bios ищет System/boot.bin с содержимым
		/// "pcos", а список установленных программ операционка восстанавливает
		/// из .exe на диске (OperatingSystem.LoadFilesFromDisk). Поэтому нам
		/// достаточно положить файлы, не трогая общие префабы накопителей.
		///
		/// Размер каждого .exe берётся из самого префаба приложения, как это
		/// делает мастер установки: new File(app.AppName + ".exe", "", false,
		/// app.size).
		/// </summary>
		private void InstallSystem(List<GameObject> hosts)
		{
			Storage disk = null;
			foreach (var host in hosts)
			{
				if (host == null) continue;
				disk = host.GetComponent<Storage>();
				if (disk != null) break;
			}

			if (disk == null)
			{
				Debug.LogWarning($"{name}: не нашлось накопителя для установки PCOS");
				return;
			}

			if (disk.files == null)
				disk.files = new List<File>();

			// Загрузчик: именно его ищет Bios, content обязан быть "pcos".
			if (!disk.ContainsFile("System/boot.bin"))
				disk.AddFile(new File("System/boot.bin", "pcos", true, systemSize));

			if (preinstalledApps == null) return;

			var catalog = Resources.LoadAll<App>("apps");

			foreach (var appName in preinstalledApps)
			{
				if (string.IsNullOrEmpty(appName)) continue;

				int size = 0;
				if (catalog != null)
				{
					foreach (var candidate in catalog)
					{
						if (candidate == null) continue;
						if (candidate.AppName != appName) continue;
						size = candidate.size;
						break;
					}
				}

				var path = appName + ".exe";
				if (disk.ContainsFile(path)) continue;

				// AddFile сам откажет, если на диске не хватает места.
				if (!disk.AddFile(new File(path, "", false, size)))
					Debug.LogWarning($"{name}: на диске нет места под '{path}'");
			}
		}

		/// <summary>
		/// Найти свободный подходящий слот и подключить деталь.
		///
		/// Слоты ищутся по всем уже собранным объектам: корпусу и каждой
		/// подключённой детали. Одним корпусом обойтись нельзя — слот
		/// Motherboard везде объявлен с setParent: 0, так что плата остаётся
		/// вне иерархии корпуса и её слоты CPU/Cooler/RAM/GPU не попадают в
		/// GetComponentsInChildren корпуса.
		/// </summary>
		private bool Attach(List<GameObject> hosts, Item item, string slotTarget)
		{
			foreach (var host in hosts)
			{
				if (host == null) continue;

				var slots = host.GetComponentsInChildren<Slot>(true);

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
			}

			return false;
		}
	}
}
