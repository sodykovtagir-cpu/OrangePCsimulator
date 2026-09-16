using System;
using System.Collections.Generic;
using UnityEngine;

public class MonitorReceiver : MonoBehaviour
{
	[Serializable]
	private struct Generation
	{
		public Transform target;

		public Canvas canvas;
	}

	[Serializable]
	public struct Monitor
	{
		public Transform target;

		public MonitorRaycaster raycaster;
	}

	[SerializeField]
	private Generation[] generations;

	[SerializeField]
	private Camera renderCameraPrefab;

	[SerializeField]
	private Vector2Int renderSize;

	[SerializeField]
	private Material renderMaterial;

	private List<Canvas> allCanvas = new List<Canvas>();

	private List<Material> mats = new List<Material>();

	public List<Monitor> targets = new List<Monitor>();

	private GameObject[] reactive;

	private bool b;

	private bool lastB;

	// Дальше этого расстояния монитор всё равно не разглядеть, а луч по всей
	// сцене стоит тем дороже, чем больше в ней предметов.
	private const float RayDistance = 20f;

	private void Start()
	{
		if (generations == null || allCanvas == null || mats == null || targets == null) return;

		for (int i = 0; i < generations.Length; i++)
		{
			var gen = generations[i];
			var target = gen.target;
			var canvas = gen.canvas;
			if (target == null || canvas == null) continue;

			var renderer = target.GetComponent<MeshRenderer>();
			Material mat = null;

			if (allCanvas.Contains(canvas))
			{
				int idx = allCanvas.IndexOf(canvas);
				if (idx >= 0 && idx < mats.Count) mat = mats[idx];
				if (renderer != null && mat != null) renderer.material = mat;
			}
			else
			{
				var cam = Instantiate(renderCameraPrefab);
				if (cam == null) continue;

				var rt = new RenderTexture(renderSize.x, renderSize.y, 100);
				cam.targetTexture = rt;

				canvas.renderMode = RenderMode.ScreenSpaceCamera;
				canvas.worldCamera = cam;

				mat = new Material(renderMaterial);
				mat.SetTexture("_MainTex", rt);

				if (renderer != null) renderer.material = mat;

				allCanvas.Add(canvas);
				mats.Add(mat);
			}

			var raycaster = canvas.GetComponent<MonitorRaycaster>();
			targets.Add(new Monitor { target = target, raycaster = raycaster });
		}
	}

	// Camera.main — это поиск по тегу MainCamera среди всех объектов сцены.
	// Вызывать его каждый кадр незачем: камера за игру не меняется, а если
	// её всё же пересоздали, ссылка станет null и мы найдём заново.
	private Camera cachedCamera;

	private void Update()
	{
		if (targets == null || targets.Count == 0) return;

		if (cachedCamera == null) cachedCamera = Camera.main;
		var cam = cachedCamera;
		if (cam == null) return;

		var mousePos = Input.mousePosition;
		var ray = cam.ScreenPointToRay(mousePos);

		b = false;

		// Луч ограничен дистанцией и слоями монитора: раньше он шёл сквозь
		// всю сцену и проверял каждый коллайдер на пути, включая детали
		// компьютеров и обломки ящиков.
		if (!Physics.Raycast(ray, out var hit, RayDistance, ~0,
			QueryTriggerInteraction.Ignore)) return;

		var tr = hit.transform;
		if (tr == null || !tr.CompareTag("Monitor") || targets == null) return;

		for (int i = 0; i < targets.Count; i++)
		{
			var monitor = targets[i];
			if (monitor.target == tr && monitor.raycaster != null)
			{
				b = true;
				monitor.raycaster.ChangeCoordinate(hit.textureCoord);
			}
		}
	}
}
