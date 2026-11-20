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
    /// Ставится на время живой доставки кастомной вещи (крышки/стекла).
    /// </summary>
    /// <remarks>
    /// Коробка выкладывает содержимое сразу в Start(), но рисунок покупатель
    /// выбирает в магазине и наносит уже после Instantiate. Поэтому на время
    /// такой доставки нужен «отложенный» режим: коробка запоминает, что
    /// показывать, а Main наносит рисунок (Release) и только потом выпускает
    /// предмет — ещё до Start(), чтобы игрок не увидел пустую коробку.
    /// </remarks>
    public static Func<GameObject> CustomDelivery { get; set; }

    /// <summary>Отложенное содержимое живой доставки (см. CustomDelivery).</summary>
    private Func<GameObject> pending;

    /// <summary>
    /// Коробка без зашитого содержимого: её наполняет код (Box_CustomCover).
    /// Обычные коробки везут свой prefab и к очереди заказов не относятся.
    /// </summary>
    public bool NeedsContent { get { return prefab == null; } }

    /// <summary>
    /// Забрать отложенное содержимое и выложить его уже с настройкой
    /// (рисунком). Вызывается сразу после Instantiate коробки — до Start().
    /// </summary>
    public void ReleaseFrom(Action<GameObject> configure)
    {
        if (pending == null && prefab == null && CustomDelivery != null)
        {
            pending = CustomDelivery;
            CustomDelivery = null;
        }
        Release(configure);
    }

    /// <summary>
    /// Готов ли предмет, который коробка ещё не выложила. По этому признаку
    /// ModForge наносит рисунок уже после создания экземпляра, но до того, как
    /// игрок увидит крышку.
    /// </summary>
    public bool Pending { get { return pending != null; } }

    /// <summary>
    /// Собрать отложенный предмет, нанести на него рисунок и выложить из
    /// коробки. Вызывается один раз — из Main, сразу после того, как доставка
    /// создала коробку (то есть ДО её Start()).
    /// </summary>
    /// <remarks>
    /// Обычный путь (без коробки) работает как раньше: содержимое спавнится в
    /// Start() коробки.
    /// </remarks>
    public void Release(Action<GameObject> configure)
    {
        var make = pending;
        if (make == null) return;
        pending = null;
        Open(make, configure);
    }

    /// <summary>Уже выложила содержимое — второй раз не надо.</summary>
    private bool opened;

    private void Start()
    {
        // Содержимое уже выложили из доставки (ReleaseFrom) — второй раз не надо.
        if (opened) return;

        // prefab == null — это коробка заказной вещи (Box_CustomCover): у неё
        // нет зашитого содержимого, его собирает код. Обычная коробка с
        // зашитым prefab от очереди заказов не зависит.
        var deferred = CustomDelivery;
        if (deferred != null && prefab == null)
        {
            pending = deferred;
            CustomDelivery = null;
        }

        // Main обычно забирает содержимое раньше (сразу после Instantiate),
        // чтобы нанести рисунок. Если его нет — выкладываем без рисунка,
        // иначе заказ пропадёт вместе с коробкой.
        if (pending != null) { Release(null); return; }

        Open();
    }

    /// <summary>Выложить содержимое коробки.</summary>
    public void Open()
    {
        Open(prefab != null ? (Func<GameObject>)(() => Instantiate(prefab, transform.position, transform.rotation)) : null, null);
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
        if (obj == null) return false;

        configure?.Invoke(obj);

        var tr = obj.transform;
        tr.Translate(position);
        tr.Rotate(rotation);
        return true;
    }
}
