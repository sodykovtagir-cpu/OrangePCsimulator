using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
	public class PrintExpert : Website
	{
		[SerializeField]
		private TextureLoader bannerPrefab;

		[SerializeField]
		private Text fileNameText;

		[SerializeField]
		private GameObject home;

		[SerializeField]
		private GameObject thankYou;

		[SerializeField]
		private Text alertText;

		[SerializeField]
		private Button purchaseButton;

		[Tooltip("Галочка «HD»: баннер 128×280 за доплату 500$ (обычный 32×70 за 200$).")]
		[SerializeField]
		private Toggle hdToggle;

		[Header("3D-превью баннера (настройки в префабе)")]
		[Tooltip("RawImage на странице заказа, куда рендерится вращающийся баннер.")]
		[SerializeField] private RawImage previewImage;
		[Tooltip("Необязательно: камера превью, если камера и Stage назначаются вручную вместо префаба.")]
		[SerializeField] private Camera previewCamera;
		[Tooltip("Необязательно: пустой Stage для баннера. Назначается вместе с previewCamera.")]
		[SerializeField] private Transform previewStage;
		[Tooltip("Скорость вращения баннера, градусов в секунду.")]
		[SerializeField] private float previewSpinSpeed = 40f;
		[Tooltip("Слой модели превью. -1 — не менять. При выборе слоя включи его в Culling Mask камеры.")]
		[SerializeField] private int previewLayer = -1;
		[Tooltip("Готовая будка с Camera, Stage и светом. Создаётся автоматически, ставить её на каждую сцену не нужно.")]
		[SerializeField] private GameObject previewRigPrefab;

		private File selectedFile;

		private GameObject previewInstance;
		private GameObject runtimeRig;
		private Camera activePreviewCamera;
		private Transform activePreviewStage;
		private Quaternion stageStartRotation;
		private bool cameraWasEnabled;
		private RenderTexture previousCameraTarget;
		private RenderTexture previewRT;
		private Texture2D previewTexture;
		private readonly List<Material> previewMaterials = new List<Material>();
		private static int nextPreviewRigSlot;

		private const int bannerPrice = 200;
		private const int hdSurcharge = 500;

		// Обычный баннер 32×70, HD — в 4 раза больше (128×280).
		private const int bannerW = 32;
		private const int bannerH = 70;
		private const int bannerHdW = 128;
		private const int bannerHdH = 280;

		private bool Hd => hdToggle != null && hdToggle.isOn;
		private int CurrentPrice => bannerPrice + (Hd ? hdSurcharge : 0);
		private int CurrentW => Hd ? bannerHdW : bannerW;
		private int CurrentH => Hd ? bannerHdH : bannerH;

		private void OnEnable()
		{
			if (selectedFile != null && (home == null || home.activeInHierarchy))
				StartPreview();
		}

		private void Update()
		{
			if (previewInstance == null || activePreviewStage == null || activePreviewCamera == null || previewRT == null)
				return;
			if (previewImage == null || !previewImage.isActiveAndEnabled || (home != null && !home.activeInHierarchy))
				return;

			// Вращаем Stage вокруг центра модели, а не смещённого pivot стойки.
			activePreviewStage.Rotate(0f, previewSpinSpeed * Time.deltaTime, 0f, Space.Self);
			activePreviewCamera.Render();
		}

		private void OnDisable()
		{
			StopPreview();
		}

		private void OnDestroy()
		{
			StopPreview();
		}

		public void SelectFile()
		{
			var o = os;
			if (o == null) return;

			System.Action<File> cb = file =>
			{
				// Страницу могли закрыть, пока был открыт выбор файла.
				if (this == null || file == null) return;
				if (fileNameText != null) fileNameText.text = file.path;
				selectedFile = file;
				if (alertText != null) alertText.text = string.Empty;
				var btn = purchaseButton;
				if (btn != null) btn.interactable = true;
				if (isActiveAndEnabled) StartPreview();
			};

			o.SelectFile(".pic", cb);
		}

		public void Purchase()
		{
			if (selectedFile == null || bannerPrefab == null) return;

			// Покупка получает свою текстуру, не ту, которую удалит StopPreview.
			var tex = LoadSelectedTexture();
			if (tex == null) return;

			int needW = CurrentW;
			int needH = CurrentH;
			if (tex.width != needW || tex.height != needH)
			{
				if (alertText != null)
					alertText.text = string.Format("Only supports {0}x{1} resolution", needW, needH);
				Destroy(tex);
				return;
			}

			var m = Main.Instance;
			if (m == null)
			{
				Destroy(tex);
				return;
			}

			int price = CurrentPrice;
			if (m.Money < price)
			{
				m.FadeText("<color=red>" + "Not enough cash" + "</color>");
				Destroy(tex);
				return;
			}

			var data = ImageConversion.EncodeToPNG(tex);
			// InstantDelivery сам создаёт экземпляр: красим именно доставленный баннер,
			// а не лишний клон, оставшийся в начале координат.
			var delivered = m.InstantDelivery(bannerPrefab.gameObject);
			if (delivered == null)
			{
				Destroy(tex);
				return;
			}

			var loader = delivered.GetComponent<TextureLoader>();
			if (loader == null)
			{
				Destroy(delivered);
				Destroy(tex);
				return;
			}

			tex.Apply(false, true);
			loader.SetTexture(tex, data);
			m.Spend(price);
			StopPreview();
			if (home != null) home.SetActive(false);
			if (thankYou != null) thankYou.SetActive(true);
		}

		private Texture2D LoadSelectedTexture()
		{
			if (selectedFile == null || string.IsNullOrEmpty(selectedFile.content)) return null;
			try
			{
				return FormatConverter.StringToTexture(selectedFile.content);
			}
			catch (System.FormatException)
			{
				if (alertText != null) alertText.text = "Invalid picture";
				return null;
			}
		}

		private void StartPreview()
		{
			StopPreview();
			// Без назначенного UI/префаба не создаём модель в комнате или внутри Canvas.
			if (previewImage == null || bannerPrefab == null || selectedFile == null) return;

			previewTexture = LoadSelectedTexture();
			if (previewTexture == null) return;
			if (!EnsureRig())
			{
				StopPreview();
				return;
			}

			const int size = 256; // То же разрешение рендера, что и в ModForge.
			previewRT = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
			previewRT.antiAliasing = 4;
			if (!previewRT.Create())
			{
				StopPreview();
				return;
			}
			activePreviewCamera.targetTexture = previewRT;

			var loader = Instantiate(bannerPrefab, activePreviewStage);
			previewInstance = loader.gameObject;
			previewInstance.transform.localPosition = Vector3.zero;
			previewInstance.transform.localRotation = Quaternion.identity;
			MakePreviewOnly();

			// materials создаёт локальные копии: общие материалы товара не изменяем.
			foreach (var renderer in previewInstance.GetComponentsInChildren<Renderer>(true))
				foreach (var material in renderer.materials)
					if (material != null && !previewMaterials.Contains(material))
						previewMaterials.Add(material);

			// Для витрины PNG/данные сохранения не нужны — только тот же TextureLoader.
			loader.SetTexture(previewTexture, null);
			FitPreviewModel();
			previewImage.texture = previewRT;
			activePreviewCamera.Render();
		}

		private bool EnsureRig()
		{
			if (previewCamera != null && previewStage != null)
			{
				activePreviewCamera = previewCamera;
				activePreviewStage = previewStage;
			}
			else if (previewRigPrefab != null)
			{
				// Разносим будки разных окон и ModForge (0, -5000, 0), даже со слоем -1.
				var position = new Vector3(500f * (++nextPreviewRigSlot), -5000f, 0f);
				runtimeRig = Instantiate(previewRigPrefab, position, Quaternion.identity);
				activePreviewCamera = runtimeRig.GetComponentInChildren<Camera>(true);
				activePreviewStage = runtimeRig.transform.Find("Stage");
			}

			if (activePreviewStage != null)
				stageStartRotation = activePreviewStage.localRotation;
			if (activePreviewCamera != null)
			{
				cameraWasEnabled = activePreviewCamera.enabled;
				previousCameraTarget = activePreviewCamera.targetTexture;
				activePreviewCamera.enabled = false; // Только явный Render, не вывод на основной экран.
			}
			return activePreviewCamera != null && activePreviewStage != null;
		}

		private void MakePreviewOnly()
		{
			if (previewLayer >= 0 && previewLayer < 32)
				foreach (var child in previewInstance.GetComponentsInChildren<Transform>(true))
					child.gameObject.layer = previewLayer;

			foreach (var body in previewInstance.GetComponentsInChildren<Rigidbody>(true))
			{
				if (!body.isKinematic)
				{
					body.velocity = Vector3.zero;
					body.angularVelocity = Vector3.zero;
				}
				body.useGravity = false;
				body.isKinematic = true;
				body.detectCollisions = false;
			}
			foreach (var collider in previewInstance.GetComponentsInChildren<Collider>(true))
				collider.enabled = false;

			// Item.Start регистрирует предмет в Main; витрина не должна попасть в игру/сейв.
			foreach (var item in previewInstance.GetComponentsInChildren<Item>(true))
			{
				item.enabled = false;
				item.hideFlags = HideFlags.DontSave;
				Destroy(item);
			}
		}

		private bool TryGetPreviewBounds(out Bounds bounds)
		{
			bounds = new Bounds();
			bool found = false;
			foreach (var renderer in previewInstance.GetComponentsInChildren<Renderer>())
			{
				if (!renderer.enabled) continue;
				if (found) bounds.Encapsulate(renderer.bounds);
				else bounds = renderer.bounds;
				found = true;
			}
			return found;
		}

		private void FitPreviewModel()
		{
			Bounds bounds;
			if (!TryGetPreviewBounds(out bounds)) return;

			// Подгоняем только баннер, не FOV, позицию или проекцию настроенной камеры.
			// Вписываем ограничивающую сферу, чтобы стойка не обрезалась при вращении.
			float depth = Vector3.Dot(activePreviewStage.position - activePreviewCamera.transform.position,
				activePreviewCamera.transform.forward);
			float radius = bounds.extents.magnitude;
			float fitRadius;
			if (activePreviewCamera.orthographic)
			{
				fitRadius = activePreviewCamera.orthographicSize * Mathf.Min(1f, activePreviewCamera.aspect);
			}
			else
			{
				float halfFov = activePreviewCamera.fieldOfView * Mathf.Deg2Rad * 0.5f;
				float halfHorizontalFov = Mathf.Atan(Mathf.Tan(halfFov) * activePreviewCamera.aspect);
				fitRadius = depth * Mathf.Sin(Mathf.Min(halfFov, halfHorizontalFov));
			}
			fitRadius = Mathf.Min(fitRadius, depth - activePreviewCamera.nearClipPlane);
			fitRadius = Mathf.Min(fitRadius, activePreviewCamera.farClipPlane - depth);
			if (radius > 0.001f && fitRadius > 0f)
				previewInstance.transform.localScale *= Mathf.Min(1f, fitRadius * 0.9f / radius);

			if (TryGetPreviewBounds(out bounds))
				previewInstance.transform.position += activePreviewStage.position - bounds.center;
		}

		private void StopPreview()
		{
			if (activePreviewCamera != null)
			{
				activePreviewCamera.targetTexture = previousCameraTarget;
				activePreviewCamera.enabled = runtimeRig == null && cameraWasEnabled;
			}
			if (activePreviewStage != null)
				activePreviewStage.localRotation = stageStartRotation;
			if (previewImage != null && previewRT != null && previewImage.texture == previewRT)
				previewImage.texture = null;

			if (previewInstance != null)
			{
				previewInstance.SetActive(false);
				Destroy(previewInstance);
				previewInstance = null;
			}
			foreach (var material in previewMaterials)
				if (material != null) Destroy(material);
			previewMaterials.Clear();
			if (previewTexture != null)
			{
				Destroy(previewTexture);
				previewTexture = null;
			}
			if (previewRT != null)
			{
				previewRT.Release();
				Destroy(previewRT);
				previewRT = null;
			}
			if (runtimeRig != null)
			{
				runtimeRig.SetActive(false);
				Destroy(runtimeRig);
				runtimeRig = null;
			}
			activePreviewCamera = null;
			activePreviewStage = null;
			previousCameraTarget = null;
		}
	}
}
