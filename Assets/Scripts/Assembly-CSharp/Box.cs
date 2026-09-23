using UnityEngine;

/// <summary>
/// Коробка, которая выкладывает свой предмет при появлении в мире.
/// </summary>
/// <remarks>
/// БАГ, который чинит этот класс. Коробка -- сама по себе предмет со своим
/// spawnId, поэтому она попадает в сохранение. При загрузке SaveManager
/// воссоздаёт и коробку, и лежавший рядом предмет по отдельности, а Start()
/// коробки вдобавок спавнил ТРЕТИЙ экземпляр. Лишний предмет появлялся без
/// регистрации в Main и висел в воздухе, потому что вставал внутрь коллайдера
/// уже существующей копии -- ровно то, на что жаловались игроки про
/// портативный монитор.
///
/// Решение: коробка выкладывает предмет только при живой доставке, а во время
/// загрузки сохранения молчит -- там содержимое уже восстановлено отдельной
/// записью.
/// </remarks>
public class Box : MonoBehaviour
{
    [SerializeField] private GameObject prefab;
    [SerializeField] private Vector3 position;
    [SerializeField] private Vector3 rotation;

    /// <summary>
    /// Идёт загрузка сохранения: коробки не должны ничего спавнить.
    /// </summary>
    /// <remarks>
    /// Статическое поле, а не параметр: коробок в сцене может быть много, и
    /// SaveManager не знает о них заранее. Флаг снимается сразу после того,
    /// как все предметы восстановлены.
    /// </remarks>
    public static bool Restoring { get; set; }

    /// <summary>Уже выложила содержимое — второй раз не надо.</summary>
    private bool opened;

    private void Start()
    {
        Open();
    }

    /// <summary>Выложить содержимое коробки.</summary>
    public void Open()
    {
        if (opened || prefab == null) return;

        // Во время загрузки содержимое коробки восстанавливается сохранением
        // как самостоятельный предмет. Спавнить копию нельзя: получится
        // дубликат, который вклинивается в коллайдер оригинала и повисает.
        if (Restoring) return;

        opened = true;

        var obj = Instantiate(prefab, transform.position, transform.rotation);
        if (obj == null) return;

        var tr = obj.transform;
        tr.Translate(position);
        tr.Rotate(rotation);
    }
}
