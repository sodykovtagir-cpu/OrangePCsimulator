using UnityEngine;

/// <summary>
/// Крышка в упаковке: покупка кладёт крышку в ящик, а не выдаёт её сразу.
/// </summary>
/// <remarks>
/// Стеклянные крышки и корпуса и так приезжают в ящике (Crate_Cover_*): ящик
/// открывают молотком, а внутри лежит предмет. Обычные крышки (чёрная, белая)
/// в магазине упаковки не имели и появлялись прямо из портала — хотя у корпуса
/// ящик есть, и игрок ждёт такую же доставку.
///
/// Правило простое: у ящика (Crate) есть крышка внутри — обычная крышка едет
/// в том же ящике, но ящик не нужно разбивать, он выкладывает её сам при
/// появлении. Кастомная крышка идёт через ModForge (Crate_Cover_ATX(Glass)),
/// поэтому этот компонент её не трогает.
/// </remarks>
[DisallowMultipleComponent]
public class CoverItem : MonoBehaviour
{
    [Tooltip("Ящик, в котором приезжает эта крышка. Пусто — подбирается по габаритам: ATX/ITX и стекло/металл.")]
    [SerializeField] private GameObject cratePrefab;

    [Tooltip("Крышка стеклянная: в каталоге она уже продаётся в ящике.")]
    [SerializeField] private bool glass;

    [Tooltip("Форм-фактор ITX (по умолчанию ATX).")]
    [SerializeField] private bool itx;

    private const string AtxCrate = "Crate_Cover_ATX(Glass)";
    private const string ItxCrate = "Crate_Cover_ITX(Glass)";

    /// <summary>Крышка уже едет в ящике — повторно упаковывать не надо.</summary>
    public bool AlreadyBoxed
    {
        get
        {
            if (glass) return true;
            var prefab = gameObject;
            return prefab != null && prefab.name.StartsWith("Crate");
        }
    }

    /// <summary>Ящик для этой крышки (или null, если подходящего нет).</summary>
    public GameObject Crate
    {
        get
        {
            if (cratePrefab != null) return cratePrefab;
            // Стеклянные крышки магазин уже продаёт в ящике — вторую упаковку
            // не надеваем.
            if (glass) return null;
            var asset = Resources.Load<GameObject>(itx ? ItxCrate : AtxCrate);
            if (asset == null)
                Debug.LogWarning("[CoverItem] Нет ящика " + (itx ? ItxCrate : AtxCrate));
            return asset;
        }
    }
}
