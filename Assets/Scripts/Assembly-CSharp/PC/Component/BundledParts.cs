using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace PC.Component
{
	/// <summary>
	/// Комплектные детали: то, что едет в коробке вместе с корпусом.
	/// </summary>
	/// <remarks>
	/// ПОЧЕМУ ДЕТАЛЬ НЕ ВЛОЖЕНА В ПРЕФАБ КОРПУСА.
	///
	/// Вложенная в префаб деталь — часть корпуса, и убрать её из мира нельзя:
	/// при каждом появлении корпуса она возникает заново, на своём месте.
	/// Игрок снимает крышку, сохраняется, загружается — и находит ДВЕ крышки:
	/// одну принёс корпус (она снова выросла из префаба), вторую восстановило
	/// сохранение как самостоятельный предмет. Ровно так крышки и
	/// размножались.
	///
	/// Здесь крышка остаётся отдельным предметом со своим spawnId: корпус
	/// выдаёт её один раз, при появлении в мире, и дальше она живёт обычной
	/// жизнью — её можно снять, унести, потерять, продать. Сохранение хранит
	/// её отдельной записью, а корпус при загрузке узнаёт свою деталь по Id
	/// (см. delivered) и потому вторую не выдаёт.
	///
	/// Деталь, восстановленная сохранением рядом со своим отсеком, ставится
	/// обратно в отсек: у загруженных деталей нет FixedJoint, и без этого
	/// крышка просто лежала бы сверху и падала при первом толчке.
	///
	/// Корпус выдаёт деталь ровно один раз за всю свою жизнь. Если игрок её
	/// потерял или продал, второй не появится: иначе потеря превращалась бы
	/// в размножение при каждой загрузке. Запасную крышку можно купить в
	/// магазине отдельной карточкой.
	/// </remarks>
	public class BundledParts : MonoBehaviour, ISave
	{
		[System.Serializable]
		public class Part
		{
			[Tooltip("Префаб комплектной детали из Resources/components")]
			public GameObject prefab;

			[Tooltip("Тег отсека, в который деталь встаёт: Cover, Drive, USB...")]
			public string slotTarget = "Cover";
		}

		[SerializeField]
		[Tooltip("Что идёт в комплекте с корпусом")]
		private Part[] parts;

		[SerializeField]
		[Tooltip("Пауза перед выдачей, сек. Слот начинает работать не сразу")]
		private float delay = 0.5f;

		/// <summary>
		/// Id деталей, которые этот корпус уже выдал.
		/// </summary>
		/// <remarks>
		/// Переживают сохранение: по ним корпус при загрузке узнаёт свою
		/// деталь и не выдаёт вторую. Список нужен именно с Id, а не с
		/// близостью к корпусу: игрок вправе унести крышку в другую комнату
		/// или спрятать её в ящик — это по-прежнему та же крышка, второй быть
		/// не должно.
		/// </remarks>
		[SerializeField]
		private List<int> delivered = new List<int>();

		// Индексы комплектных деталей, уже выданных этим экземпляром корпуса.
		// Id у Item назначается только в Start, поэтому одним списком delivered
		// нельзя надёжно определить, что крышка уже была создана.
		private readonly HashSet<int> issuedParts = new HashSet<int>();
		private bool restored;

		private IEnumerator Start()
		{
			// В старых собранных вручную префабах бывает два BundledParts.
			// Только первый имеет право выдавать предметы.
			if (GetComponents<BundledParts>()[0] != this) yield break;
			if (delay > 0f) yield return new WaitForSeconds(delay);
			Deliver();
		}

		/// <summary>Выдать всё, чего у корпуса ещё нет.</summary>
		public void Deliver()
		{
			if (parts == null) return;

			if (GetComponents<BundledParts>()[0] != this) return;
			for (int i = 0; i < parts.Length; i++)
				Deliver(parts[i], i);
		}

		private void Deliver(Part part, int index)
		{
			if (part == null || part.prefab == null) return;

			var sample = part.prefab.GetComponent<Item>();
			if (sample == null)
			{
				Debug.LogWarning($"{name}: у комплектной детали {part.prefab.name} нет Item");
				return;
			}

			var slot = FindSlot(part.slotTarget);
			if (slot == null)
			{
				Debug.LogWarning($"{name}: не нашёлся отсек {part.slotTarget}");
				return;
			}

			// В отсеке уже что-то стоит: вторую крышку выдавать некуда.
			if (slot.IsUsing) return;

			var existing = FindDelivered(sample, slot);
			if (existing != null)
			{
				issuedParts.Add(index);
				Remember(existing);

				// Деталь лежит ровно на своём месте — значит её только что
				// восстановило сохранение. Ставим обратно в отсек: крепление
				// (FixedJoint и Connector) сохранением не переносится.
				if (InPlace(existing, slot)) slot.TryAttach(existing);
				return;
			}

			// Деталь уже выдавалась, но её больше нет в мире: игрок её
			// потерял, утопил или продал. Второй раз корпус не выдаёт —
			// иначе снятая деталь снова размножалась бы при загрузке.
			// Потерянную крышку игрок купит запасной в магазине.
			// Любой восстановленный из сейва корпус уже выдавал комплект.
			// Даже старый сейв с пустым delivered НЕ даёт разрешения выдать
			// новую крышку: снятая может лежать далеко или быть продана.
			if (restored || issuedParts.Contains(index)) return;
			issuedParts.Add(index);

			var obj = Instantiate(part.prefab);
			var item = obj != null ? obj.GetComponent<Item>() : null;
			if (item == null)
			{
				if (obj != null) Destroy(obj);
				return;
			}

			if (!slot.TryAttach(item))
			{
				// Отсек деталь не принял (тег или формат). Оставляем её рядом
				// с корпусом: вещь в комплекте, пусть игрок поставит сам.
				Debug.LogWarning($"{name}: деталь {item.SpawnId} не встала в отсек {part.slotTarget}");
				obj.transform.position = Mouth(slot) + Vector3.up * (Size(slot) + 0.05f);
				Remember(item);
				return;
			}

			Remember(item);
		}

		/// <summary>
		/// Уже выданная деталь этого вида: своя из сохранения или такая же
		/// рядом со своим отсеком.
		/// </summary>
		private Item FindDelivered(Item sample, Slot slot)
		{
			var main = Main.Instance;
			if (main != null && delivered != null)
			{
				for (int i = 0; i < delivered.Count; i++)
				{
					var known = main.GetItemById(delivered[i]);
					if (known != null && SameKind(known, sample)) return known;
				}
			}

			// Записи нет: сохранение старое, либо деталь восстановили под
			// другим Id. Ищем такую же деталь рядом с отсеком — она отсюда.
			var all = FindObjectsOfType<Item>();
			if (all == null) return null;

			float radius = Mathf.Max(Size(slot) * 3f, 0.5f);
			float limit = radius * radius;

			Item nearest = null;
			float best = float.MaxValue;

			for (int i = 0; i < all.Length; i++)
			{
				var it = all[i];
				if (it == null || !SameKind(it, sample)) continue;

				if (it.transform.IsChildOf(transform)) return it;

				float d = (it.transform.position - Mouth(slot)).sqrMagnitude;
				if (d > limit || d >= best) continue;

				best = d;
				nearest = it;
			}

			return nearest;
		}

		/// <summary>Деталь стоит там, куда её ставит отсек.</summary>
		private static bool InPlace(Item item, Slot slot)
		{
			float snap = Mathf.Clamp(Size(slot) * 0.25f, 0.05f, 0.2f);
			return (item.transform.position - Mouth(slot)).sqrMagnitude <= snap * snap;
		}

		private Slot FindSlot(string target)
		{
			if (string.IsNullOrEmpty(target)) return null;

			var slots = GetComponentsInChildren<Slot>(true);
			if (slots == null) return null;

			for (int i = 0; i < slots.Length; i++)
			{
				var s = slots[i];
				if (s != null && s.target == target) return s;
			}

			return null;
		}

		private static bool SameKind(Item a, Item b)
		{
			if (a == null || b == null) return false;

			string x = a.SpawnId;
			string y = b.SpawnId;

			if (string.IsNullOrEmpty(x) || string.IsNullOrEmpty(y))
				return string.Equals(a.name, b.name, System.StringComparison.OrdinalIgnoreCase);

			return string.Equals(x, y, System.StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>Середина отсека: сюда деталь и встаёт.</summary>
		private static Vector3 Mouth(Slot slot)
		{
			var col = slot.GetComponent<Collider>();
			return col != null ? col.bounds.center : slot.transform.position;
		}

		/// <summary>Размер отсека: по нему и «рядом», и «на месте».</summary>
		private static float Size(Slot slot)
		{
			var col = slot.GetComponent<Collider>();
			if (col == null) return 0.25f;

			var e = col.bounds.extents;
			return Mathf.Max(e.x, e.y, e.z);
		}

		private void Remember(Item item)
		{
			if (item == null) return;
			if (item.Id == 0 && Main.Instance != null)
				item.Id = Main.Instance.GetNewId(item);
			if (item.Id == 0) return;
			if (delivered == null) delivered = new List<int>();
			if (delivered.Contains(item.Id)) return;

			delivered.Add(item.Id);
		}

		public void ToData(JObject jObject)
		{
			if (jObject == null) return;

			var arr = new JArray();
			if (delivered != null)
			{
				for (int i = 0; i < delivered.Count; i++)
					arr.Add(delivered[i]);
			}

			jObject["bundled"] = arr;
		}

		public void FromData(JObject jObject)
		{
			if (jObject == null) return;

			// Даже старый сейв с пустым bundled — это уже существующий
			// корпус, повторную выдачу делать нельзя.
			restored = true;
			delivered = new List<int>();

			var arr = jObject["bundled"] as JArray;
			if (arr == null) return;

			for (int i = 0; i < arr.Count; i++)
			{
				var token = arr[i];
				if (token == null || token.Type == JTokenType.Null) continue;

				int id = token.ToObject<int>();
				if (id == 0 || delivered.Contains(id)) continue;

				delivered.Add(id);
			}
		}
	}
}
