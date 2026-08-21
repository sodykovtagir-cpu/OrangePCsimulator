using UnityEngine;

public class CustomGlass : MonoBehaviour
{
    [Tooltip("Материал стекла (должен поддерживать прозрачность и текстуру)")]
    public Material glassMaterialPrefab;

    private Material runtimeMaterial;
    private Renderer objectRenderer;

    private void Awake()
    {
        objectRenderer = GetComponent<Renderer>();
        if (objectRenderer == null || glassMaterialPrefab == null)
        {
            Debug.LogWarning($"{name}: CustomGlass требует Renderer и назначенный Glass Material Prefab.");
            enabled = false;
            return;
        }
        // Создаем инстанс материала, чтобы не менять материал у всех стекол в игре сразу
        runtimeMaterial = new Material(glassMaterialPrefab);
        objectRenderer.material = runtimeMaterial;
    }

    private void OnDestroy()
    {
        // Инстанс материала никто не освобождает за нас — иначе утечка на каждой загрузке сцены.
        if (runtimeMaterial != null)
            Destroy(runtimeMaterial);
    }

    /// <summary>
    /// Вызывается, когда игрок заказывает стекло на сайте
    /// </summary>
    /// <param name="designTexture">Текстура из Пейнта (.pic)</param>
    /// <param name="tintColor">Выбранный цвет стекла</param>
    public void ApplyDesign(Texture2D designTexture, Color tintColor)
    {
        if (runtimeMaterial == null) return;

        // Применяем картинку (наклейку)
        if (designTexture != null)
        {
            runtimeMaterial.SetTexture("_MainTex", designTexture); // Или _Albedo, _BaseMap в зависимости от шейдера
        }

        // Применяем цвет (тонировка)
        runtimeMaterial.SetColor("_Color", tintColor); // Или _Tint, _BaseColor
    }
}