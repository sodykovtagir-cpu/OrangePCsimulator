using System.Collections;
using UnityEngine;

namespace PC
{
	/// <summary>
	/// Выдать готовую сборку из вскрытого ящика. Серверные ящики
    /// сохраняют столкновения с обломками; старым ПК/майнерам оставлена
    /// опциональная защита для их прежних тесных ящиков.
	///
	/// Штатный Box создаёт предмет ровно в точке ящика. Для обычных товаров
	/// это работает, но рама BigMiner — 2.92 x 10.54 x 2.35, а вскрытый ящик
	/// к этому моменту уже превратился в шесть обломков Part (тег Box,
	/// Rigidbody mass 1), стоящих в той же точке. Рама рождается ВНУТРИ них,
	/// обломки её выталкивают и срывают видеокарты.
	///
	/// Увеличить ящик до вмещающего нельзя: раме нужен масштаб x2.45, это
	/// 11.03 в высоту, а товар приезжает порталом с высоты 8 и такой ящик
	/// упирается в потолок.
	///
	/// Поэтому обломки убираются с дороги: их коллизия с грузом отключается
	/// (Physics.IgnoreCollision), а сами они живут обычной жизнью и разлетаются
	/// как всегда. Груз при этом остаётся полноценным физическим телом —
	/// падает на пол и укладывается сам, как ванильный Table, который торчит
	/// из своего ящика на 1.37 и никому не мешает.
	/// </summary>
	public class ReadyBuildBox : MonoBehaviour
	{
		[SerializeField]
		[Tooltip("Что выдать при вскрытии ящика")]
		private GameObject prefab;

		[SerializeField]
		[Tooltip("Смещение относительно ящика")]
		private Vector3 position;

		[SerializeField]
		[Tooltip("Доворот относительно ящика")]
		private Vector3 rotation;

        [SerializeField]
        [Tooltip("Отключать столкновения с содержимым. Для серверных ящиков подходящего размера — выключено.")]
        private bool ignoreDebrisCollisions = true;

		private void Start()
		{
			if (prefab == null) return;

			var obj = Instantiate(prefab, transform.position, transform.rotation);
			if (obj == null) return;

			var tr = obj.transform;
			tr.Translate(position);
			tr.Rotate(rotation);

            // Server crates are authored to fit the payload. Keep normal
            // debris contacts, for both a bare chassis and a ready-PC plan.
            if (!ignoreDebrisCollisions) return;

			// A ready-PC plan spawns its case/parts as independent roots.
            // Scanning the plan's children never sees those colliders. Hand
            // the debris to the shared builder before its Start can run, so
            // every new part is protected immediately, before physics.
            var builder = obj.GetComponent<ReadyBuildSpawner>();
            if (builder != null)
                builder.DeliveryDebris = GetComponentsInChildren<Collider>(true);
            else
                StartCoroutine(FreeFromDebris(obj));
		}

		/// <summary>
		/// Развести груз и обломки ящика, чтобы они не выталкивали друг друга.
		///
		/// Сборка появляется не мгновенно: ReadyBuildSpawner ставит детали по
		/// одной за несколько кадров. Поэтому список коллайдеров груза
		/// пересобирается несколько раз подряд — иначе видеокарты, приехавшие
		/// позже, снова столкнутся с обломками.
		/// </summary>
		private IEnumerator FreeFromDebris(GameObject content)
		{
			var debris = new System.Collections.Generic.List<Collider>();
			foreach (var col in GetComponentsInChildren<Collider>(true))
			{
				if (col != null) debris.Add(col);
			}
			if (debris.Count == 0) yield break;

			// Пока спавнер доставляет детали, а обломки ещё рядом.
			for (int pass = 0; pass < 40; pass++)
			{
				if (content == null) yield break;

				foreach (var own in content.GetComponentsInChildren<Collider>(true))
				{
					if (own == null) continue;
					foreach (var other in debris)
					{
						if (other == null) continue;
						Physics.IgnoreCollision(own, other, true);
					}
				}

				yield return new WaitForSeconds(0.1f);
			}
		}
	}
}
