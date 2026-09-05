using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Делает панель (Image) похожей на «матовое стекло»: полупрозрачная подложка
/// с лёгким шумом-зерном поверх. Работает в URP без GrabPass/захвата экрана,
/// поэтому безопасно для мобильной сборки и для рендера в RenderTexture.
/// Вешается на тот же объект, что и Image (фон панели задач / меню Пуск).
/// </summary>
[RequireComponent(typeof(Image))]
[ExecuteAlways]
public class FrostedGlass : MonoBehaviour
{
    [Range(0f, 1f)] public float opacity = 0.72f;
    public Color tint = new Color(0.92f, 0.94f, 0.98f, 1f);
    [Range(0f, 0.5f)] public float grain = 0.06f;
    public float noiseScale = 120f;

    private static Material sharedGlass;

    private Image image;

    private void Awake()
    {
        Apply();
    }

    private void OnEnable()
    {
        Apply();
    }

    private void Reset()
    {
        Apply();
    }

    public void Apply()
    {
        image = GetComponent<Image>();
        if (image == null) return;

        if (sharedGlass == null)
        {
            var shader = Shader.Find("UI/FrostedGlass");
            if (shader != null)
            {
                sharedGlass = new Material(shader);
                sharedGlass.name = "UIFrostedGlass_Runtime";
            }
        }

        if (sharedGlass != null)
        {
            image.material = sharedGlass;
            image.material.SetColor("_Color", new Color(tint.r, tint.g, tint.b, opacity));
            image.material.SetFloat("_NoiseAmount", grain);
            image.material.SetFloat("_NoiseScale", noiseScale);
            // Чтобы цвет/альфа шли из материала, а не из Image.
            image.color = Color.white;
        }
        else
        {
            // Фолбэк, если шейдер не найден: просто полупрозрачный фон.
            image.color = new Color(tint.r, tint.g, tint.b, opacity);
        }
    }
}
