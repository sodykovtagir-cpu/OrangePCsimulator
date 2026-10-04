using UnityEngine;

namespace PC.Component.Software
{
	[AddComponentMenu("3DMork/Flyby Waypoint")]
	public class FlybyWaypoint : MonoBehaviour
	{
		[Tooltip("Название фазы бенчмарка в верхнем правом углу (если пусто, генерируется автоматически)")]
		public string phaseName = "";

		[Tooltip("Длительность пролёта от этой точки до следующей (в секундах)")]
		public float duration = 2.5f;

		[Tooltip("Множитель нагрузки на видеокарту/процессор в этой сцене")]
		public float loadMultiplier = 1f;

		[Tooltip("Опциональная цель взгляда камеры. Если не назначена, камера использует собственный поворот (rotation) этой точки")]
		public Transform lookAtTarget;

		[Tooltip("Использовать ориентацию (вращение) этого Transform для направления камеры, если lookAtTarget не задан")]
		public bool useTransformRotation = true;

		private void OnDrawGizmos()
		{
			Gizmos.color = Color.cyan;
			Gizmos.DrawWireSphere(transform.position, 0.2f);
			Gizmos.color = Color.yellow;
			Gizmos.DrawRay(transform.position, transform.forward * 0.8f);

			if (lookAtTarget != null)
			{
				Gizmos.color = Color.red;
				Gizmos.DrawLine(transform.position, lookAtTarget.position);
			}
		}
	}
}
