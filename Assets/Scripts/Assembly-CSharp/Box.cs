using System;
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

    /// <summary>
    /// Чем наполняется ящик/коробка в живой доставке заказной вещи.
    /// </summary>
    /// <remarks>
    /// Доставка создаёт ящик раньше, чем Main успевает подготовить предмет с
    /// рисунком, поэтому способ сборки содержимого живёт в статическом поле:
    /// ящик забирает его в Start(). Порядок заказов сохраняет Main.
    /// </remarks>
    public static Func<GameObject> CustomDelivery { get; set; }

    /// <summary>Отложенный способ сборки содержимого (см. CustomDelivery).</summary>
    private Func<GameObject> pending;

    /// <summary>
    /// У коробки нет зашитого предмета: содержимое собирает код. У ящика с
    /// зашитым содержимым (например, обычного стекла) это false, поэтому
    /// чужие заказы его не трогают.
    /// </summary>
    public bool NeedsContent { get { return prefab == null && contentFactory == null; } }

    /// <summary>Содержимое ещё не выложено: можно наполнять.</summary>
    public bool Pending { get { return pending != null; } }

    /// <summary>
    /// Готов ли заказ, которому нужна именно эта коробка: содержимое не
    /// выложено, а код уже знает, что сюда класть.
    /// </summary>
    public bool AwaitingContent { get { return awaiting; } }

    /// <summary>Пометить коробку под заказ (до того, как в неё положат предмет).</summary>
    public void ExpectContent()
    {
        awaiting = true;
        prefab = null;
    }

    /// <summary>
    /// Положить в коробку предмет и нанести на него настройку (рисунок).
    /// Содержимое остаётся отложенным до Start() коробки: у ящика стекла Box
    /// лежит в выключенном BrokenCrate, поэтому Start() случается, когда ящик
    /// разбивают молотком. До этого ящик стоит целым, как обычная коробка.
    /// </summary>
    public void FillWith(Func<GameObject> make, Action<GameObject> configure)
    {
        awaiting = false;
        contentFactory = configure;
        pending = make;
    }

    /// <summary>
    /// Досрочная выкладка предмета (например, ящик разбили раньше, чем
    /// доставка успела его наполнить): берём отложенное содержимое, если оно
    /// есть, и выкладываем без дополнительной настройки.
    /// </summary>
    public void Release(Action<GameObject> configure)
    {
        if (pending == null && prefab == null && CustomDelivery != null)
        {
            pending = CustomDelivery;
            CustomDelivery = null;
        }
        var make = pending;
        if (make == null) return;
        pending = null;
        Open(make, configure ?? contentFactory);
    }

    private Action<GameObject> contentFactory;
    private bool awaiting;

    /// <summary>Содержимое уже выложено — второй раз не надо.</summary>
    private bool opened;

    private void Start()
    {
        // Содержимое уже выложили из доставки — второй раз не надо.
        if (opened) return;

        // Заказ ждёт рисунка: сам ничего не выкладываем.
        if (awaiting) return;

        // Коробка заказной вещи без зашитого предмета: содержимое даёт код.
        var deferred = CustomDelivery;
        if (deferred != null && prefab == null)
        {
            pending = deferred;
            CustomDelivery = null;
        }

        // Если доставка подготовила предмет — выкладываем его сейчас, иначе
        // заказ пропал бы вместе с коробкой.
        if (pending != null) { Release(null); return; }

        Open();
    }

    /// <summary>Выложить содержимое коробки.</summary>
    public void Open()
    {
        // Ящик с зашитым содержимым, если живого заказа нет.
        if (prefab != null) pending = pending ?? (() => Instantiate(prefab, transform.position, transform.rotation));
        Release(null);
    }

    private bool Open(Func<GameObject> make, Action<GameObject> configure)
    {
        if (opened || make == null) return false;

        // Во время загрузки содержимое коробки восстанавливается сохранением
        // как самостоятельный предмет. Спавнить копию нельзя: получится
        // дубликат, который вклинивается в коллайдер оригинала и повисает.
        if (Restoring) return false;

        opened = true;

        var obj = make();
        // У адресного наполнения (ContentFactory) предмет создаёт сам код.
        if (obj == null && contentFactory == null) return false;

        configure?.Invoke(obj);

        if (obj != null)
        {
            var tr = obj.transform;
            tr.Translate(position);
            tr.Rotate(rotation);
        }
        return true;
    }
}
