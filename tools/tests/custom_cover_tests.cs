// Contracts for «кастомная и обычная крышки приезжают в ящике» and
// «пароль Wi-Fi скрыт звёздочками». Настоящие Box.cs и CustomPaint.cs
// компилируются вместе с заглушками движка, путь доставки прогоняется.
using System;
using System.Reflection;
using UnityEngine;

internal static class Program
{
    private static int failures;

    private static void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "  ok   " : "  FAIL ") + what);
        if (!ok) failures++;
    }

    // Ящик стекла как в Assets/Resources/components/Crate_Cover_ATX(Glass).prefab:
    // на корне только Crate, а Box лежит в выключенном BrokenCrate.
    private static GameObject NewCratePrefab(string name)
    {
        var go = new GameObject(name);
        var crate = go.AddComponent<Crate>();
        var broken = new GameObject("BrokenCrate");
        broken.activeSelf = false;
        go.AddChild(broken);
        broken.AddComponent<Box>();
        crate.broken = broken;
        return go;
    }

    // Игрок ударил ящик молотком: Crate включает BrokenCrate, Unity вызывает
    // Start() его Box.
    private static void BreakCrate(GameObject live)
    {
        var crate = live.GetComponent<Crate>();
        var box = ContentBox(live);
        // Заглушка копирует компоненты детей, но не ссылки внутри них,
        // поэтому BrokenCrate у клона ищем так же, как в префабе.
        if (crate.broken == null) crate.broken = box.gameObject;
        // В Unity SetParent(null) сохраняет мировую позу, заглушка — нет.
        box.transform.position = live.transform.position;
        box.transform.rotation = live.transform.rotation;
        crate.BreakApart();
        BoxTestHooks.Start(box);
    }

    // Box, который выкладывает содержимое ящика: корень или выключенный BrokenCrate.
    private static Box ContentBox(GameObject crate)
    {
        var box = crate.GetComponent<Box>();
        return box != null ? box : crate.GetComponentInChildren<Box>(true);
    }

    private static GameObject NewCoverPrefab(string name)
    {
        var go = new GameObject(name);
        go.AddComponent<Renderer>();
        go.AddComponent<CustomPaint>();
        return go;
    }

    private static void TestCustomCoverInCrate()
    {
        Console.WriteLine("[1] Заказ кастомной крышки -> её ящик -> стекло с рисунком");
        var m = GameMain.Create();
        Box.Restoring = false;
        Box.CustomDelivery = (Func<GameObject>)null;
        int start = UnityEngine.Object.InstantiateCount;

        var cratePrefab = NewCratePrefab("Crate_Cover_ATX(Glass)");
        var itemPrefab = NewCoverPrefab("cover_atx_glass_custom");
        var texture = new Texture2D(128, 128);
        bool painted = false;

        bool ok = m.DeliverCustom(itemPrefab, cratePrefab, go =>
        {
            var paint = go.GetComponent<CustomPaint>();
            if (paint == null) return;
            paint.SetTexture(texture, new byte[] { 7 });
            painted = true;
        });

        Check(ok, "заказ принят");
        Check(m.Queue.Count == 1 && m.Queue[0] == cratePrefab, "в доставку ушёл ящик стекла, а не голая крышка");
        Check(UnityEngine.Object.InstantiateCount == start, "крышка не появляется сразу, без ящика");
        Check(!painted, "рисунок не наносится в момент заказа");

        var live = m.SpawnFromQueue(cratePrefab);
        Check(!painted, "пока ящик целый, рисунок не наносится");
        Check(UnityEngine.Object.InstantiateCount == start + 1, "создан только ящик — он приехал целым");
        Check(live.GetComponent<Crate>() != null && !live.GetComponent<Crate>().Opened, "ящик не открыт сам по себе");
        Check(ContentBox(live) != null && !BoxTestHooks.OpenedOnce(ContentBox(live)),
              "содержимое ящика ещё не выложено");

        // Пока ящик лежал, он успел сдвинуться с точки доставки.
        live.transform.position = new Vector3(4f, 1f, 4f);

        BreakCrate(live);
        Check(painted, "стекло покрашено в момент, когда ящик разбили");
        Check(UnityEngine.Object.InstantiateCount == start + 2, "разбитый ящик выложил стекло");
        Check(UnityEngine.Object.LastInstantiatePosition.x == 4f && UnityEngine.Object.LastInstantiatePosition.z == 4f,
              "стекло выпало у разбитого ящика, а не в точке доставки");
        Check(live.GetComponent<Crate>().Opened, "ящик открылся ударом молотка");
        Check(ContentBox(live) != null && BoxTestHooks.OpenedOnce(ContentBox(live)),
              "Box внутри BrokenCrate перехвачен: обычное стекло не выкладывается");
        Check(m.CustomQueuesEmpty, "очереди заказов пусты");
    }

    private static void TestTwoOrders()
    {
        Console.WriteLine("[2] Два заказа подряд не путают рисунки");
        var m = GameMain.Create();
        Box.Restoring = false;
        Box.CustomDelivery = (Func<GameObject>)null;
        var atxCrate = NewCratePrefab("Crate_Cover_ATX(Glass)");
        var itxCrate = NewCratePrefab("Crate_Cover_ITX(Glass)");
        var first = NewCoverPrefab("cover_atx_glass_custom");
        var second = NewCoverPrefab("cover_itx_glass_custom");
        string paintedFor = null;

        m.DeliverCustom(first, atxCrate, go => paintedFor = "A:" + (go.name ?? ""));
        m.DeliverCustom(second, itxCrate, go => paintedFor = "B:" + (go.name ?? ""));

        Check(m.Queue.Count == 2, "оба ящика в очереди доставки");
        var atxLive = m.SpawnFromQueue(atxCrate);
        Check(paintedFor == null, "пока ящики целые, рисунки не нанесены");
        BreakCrate(atxLive);
        Check(paintedFor == "A:cover_atx_glass_custom_copy", "первый ящик получил первую крышку");
        var itxLive = m.SpawnFromQueue(itxCrate);
        Check(paintedFor == "A:cover_atx_glass_custom_copy", "второй ящик сам по себе ничего не покрасил");
        BreakCrate(itxLive);
        Check(paintedFor == "B:cover_itx_glass_custom_copy", "второй ящик получил вторую крышку");
    }

    private static void TestPlainCoverNoCrate()
    {
        Console.WriteLine("[3] Обычная (не стеклянная) крышка — вообще без ящика");
        var main = Read("Assets/Scripts/Assembly-CSharp/Main.cs");
        var delivery = main.Substring(main.IndexOf("public void Delivery(GameObject prefab, bool large)"));
        delivery = delivery.Substring(0, delivery.IndexOf("\n\t}"));
        Check(delivery.Contains("IsPlainCover(prefab)"), "обычная крышка распознаётся в доставке");
        Check(delivery.Contains("InstantDelivery(prefab)"), "такая крышка выдаётся сразу, без упаковки");
        Check(!delivery.Contains("caseCrate") && !delivery.Contains("Delivery(crate)"),
              "для обычной крышки ящик не подставляется");
        Check(!delivery.Contains("GetComponent<CoverItem>"), "упаковки обычной крышки в доставке нет");
        Check(main.Contains("private static bool IsPlainCover(GameObject prefab)") &&
              main.Contains("StartsWith(\"Cover_\")"),
              "признак обычной крышки — префаб Cover_*, а не стеклянный Crate_*");
        Check(!System.IO.File.Exists(System.IO.Path.Combine(Root, "Assets/Scripts/Assembly-CSharp/CoverItem.cs")),
              "компонент CoverItem (упаковка обычных крышек) удалён");

        // ModForge: обычные (не стеклянные) крышки тоже без ящика.
        var forge = Read("Assets/Scripts/Assembly-CSharp/PC/Component/Software/ModForge.cs");
        Check(forge.Contains("null,                            // cover_atx_custom") &&
              forge.Contains("null,                            // cover_itx_custom"),
              "у простых крышек в ModForge нет ящика — сразу у портала");
        Check(forge.Contains("if (name == null) return null;"),
              "для простой крышки CaseCrate возвращает пусто");
        Check(!forge.Contains("if (box == null) return false;"),
              "отсутствие ящика не отменяет заказ — предмет всё равно доставляется");
    }

    private static void TestRestoringSilence()
    {
        Console.WriteLine("[4] Загрузка сейва не плодит дубликаты");
        var m = GameMain.Create();
        Box.Restoring = true;
        Box.CustomDelivery = (Func<GameObject>)null;
        var cratePrefab = NewCratePrefab("Crate_Cover_ATX(Glass)");
        m.DeliverCustom(NewCoverPrefab("cover_atx_glass_custom"), cratePrefab, go => { });
        int before = UnityEngine.Object.InstantiateCount;
        var live = m.SpawnFromQueue(cratePrefab);
        BreakCrate(live);
        Check(UnityEngine.Object.InstantiateCount == before + 1, "во время Restoring создан только сам ящик");
        Box.Restoring = false;

        var plainBox = new GameObject("Box_CPU").AddComponent<Box>();
        BoxTestHooks.SetPrefab(plainBox, new GameObject("cpu"));
        int n = UnityEngine.Object.InstantiateCount;
        BoxTestHooks.Start(plainBox);
        Check(UnityEngine.Object.InstantiateCount == n + 1, "обычная коробка выкладывает содержимое");
        plainBox.Open();
        Check(UnityEngine.Object.InstantiateCount == n + 1, "повторный Open() не плодит копии");
    }

    private static void TestSources()
    {
        Console.WriteLine("[5] Покупка идёт через ящик стекла");
        var forge = Read("Assets/Scripts/Assembly-CSharp/PC/Component/Software/ModForge.cs");
        Check(forge.Contains("Main.Instance.DeliverCustom("), "покупка идёт через DeliverCustom");
        Check(forge.Contains("Crate_Cover_ATX(Glass)") && forge.Contains("Crate_Cover_ITX(Glass)"),
              "ящик — тот же, в котором приезжает обычное стекло");
        Check(forge.Contains("Resources.Load<GameObject>(\"components/\" + name)"), "ящик берётся из Resources");
        Check(forge.Contains("caseCratePrefab"), "ящик можно назначить в инспекторе");
        Check(forge.Contains("m.AddMoney(price)"), "неудачная доставка возвращает деньги");
        Check(!forge.Contains("InstantDelivery") && !forge.Contains("Box_CustomCover"),
              "самодельная коробка и мгновенная выдача убраны");

        var main = Read("Assets/Scripts/Assembly-CSharp/Main.cs");
        Check(!main.Contains("crate.BreakApart()"),
              "ящик заказа не открывается сам — его разбивают молотком, как обычный");
        Check(main.Contains("box.transform.position"),
              "позиция стекла берётся у ящика в момент поломки, а не у точки доставки");
        Check(main.Contains("GetComponentInChildren<Box>(true)"),
              "Box ящика ищется и в выключенном BrokenCrate (как в реальном префабе)");

        var boxSrc = Read("Assets/Scripts/Assembly-CSharp/Box.cs");
        var fill = boxSrc.Substring(boxSrc.IndexOf("public void FillWith"));
        fill = fill.Substring(0, fill.IndexOf("\n    }"));
        Check(!fill.Contains("Release(null)"),
              "FillWith не выкладывает предмет сразу — ждёт Start() разбитого ящика");
        Check(main.Contains("private Transform doorPoint;") && main.Contains("CustomDeliveryPoint"),
              "есть запасная точка доставки, если packagePoint пуст");
        Check(!main.Contains("коробка появится сразу у портала"), "старый варнинг про пустой packagePoint убран");
        Check(main.Contains("NotifySpawned(Instantiate(original, packagePoint.position"), "очередь доставки сообщает о появлении");
        Check(main.Contains("if (Box.Restoring)"), "загрузка сейва учтена");

        var crate = Read("Assets/Scripts/Assembly-CSharp/Crate.cs");
        Check(crate.Contains("public void BreakApart()") && crate.Contains("public bool CanOpen()"),
              "ящик умеет открываться программно");
    }

    private static void TestWifiMasked()
    {
        Console.WriteLine("[6] Пароль Wi-Fi — звёздочки");
        var entry = Read("Assets/Scripts/Assembly-CSharp/PC/Component/Software/OS/WifiNetworkEntry.cs");
        Check(entry.Contains("private void PreparePasswordField()"), "единая точка настройки поля пароля");
        Check(entry.Contains("password.contentType = InputField.ContentType.Password"), "тип Password ставится в рантайме");
        Check(entry.Contains("password.asteriskChar = '*'"), "символ маски задан явно");
        Check(entry.Contains("private void Awake()"), "маска включена ещё до Initialize");
        var prefab = Read("Assets/Scripts/Assembly-CSharp/PC/Component/WifiPanelPrefab.prefab");
        Check(prefab.Contains("m_ContentType: 7") && prefab.Contains("m_InputType: 2"),
              "в префабе поле пароля — ContentType.Password (7), InputType.Password (2)");
        Check(!prefab.Contains("m_ContentType: 0"), "в префабе не осталось Standard-поля");
        Check((int)UnityInputType.Password == 2 && (int)UnityInputType.Pin == 8,
              "значения InputType как в Unity");
        Check((int)UnityContentType.Password == 7, "ContentType.Password == 7");

        var pages = Read("Assets/Scripts/Assembly-CSharp/PC/Component/Software/RouterWebPages.cs");
        Check(pages.Contains("private static void Secure(InputField input)"), "общий помощник Secure()");
        Check(Count(pages, "Secure(key)") == 1 && Count(pages, "Secure(parentKey)") == 1,
              "оба поля пароля на сайте роутера пропущены через Secure()");
    }

    private static void TestAssets()
    {
        Console.WriteLine("[7] Ассеты");
        var atx = Read("Assets/Resources/components/Crate_Cover_ATX(Glass).prefab");
        Check(atx.Contains("m_TagString: Crate"), "ящик стекла помечен тегом Crate");
        Check(atx.Contains("guid: 46655a7f7503170198a223c11988a65a"), "внутри ящика лежит Crate-скрипт");
        Check(atx.Contains("guid: d7f903f50f13e21af61667d85cf95dde"), "внутри ящика есть Box, который выкладывает стекло");
        var forge = Read("Assets/GameObject/ModForge.prefab");
        Check(forge.Contains("caseCratePrefab: {fileID: 114145850920321883, guid: 0bd7f325f7c3ab148bd437fbeff45c2c"),
              "в ModForge.prefab прописан ящик стекла");
        var main = Read("Assets/Scripts/Assembly-CSharp/Main.cs");
        Check(main.Contains("public event Action<GameObject> Spawned"), "событие появления доставки объявлено");
        Check(!System.IO.File.Exists(System.IO.Path.Combine(Root, "Assets/GameObject/Box_CustomCover.prefab")),
              "самодельная Box_CustomCover удалена");
    }

    private static int Count(string haystack, string needle)
    {
        int n = 0, at = 0;
        while ((at = haystack.IndexOf(needle, at, StringComparison.Ordinal)) >= 0) { n++; at += needle.Length; }
        return n;
    }

    private static string Read(string relative)
    {
        return System.IO.File.ReadAllText(System.IO.Path.Combine(Root, relative));
    }

    private static string _root;
    private static string Root
    {
        get
        {
            if (_root != null) return _root;
            var dir = System.IO.Directory.GetCurrentDirectory();
            while (!System.IO.Directory.Exists(System.IO.Path.Combine(dir, "Assets")) &&
                   System.IO.Path.GetDirectoryName(dir) != dir)
                dir = System.IO.Path.GetDirectoryName(dir);
            _root = dir;
            return _root;
        }
    }

    public static int Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        TestCustomCoverInCrate();
        TestTwoOrders();
        TestPlainCoverNoCrate();
        TestRestoringSilence();
        TestSources();
        TestWifiMasked();
        TestAssets();
        Console.WriteLine(failures == 0
            ? "PASS: custom cover ships in the glass crate, plain covers ship without a crate, Wi-Fi password is masked"
            : "FAILED: " + failures);
        return failures == 0 ? 0 : 1;
    }
}

internal enum UnityContentType { Standard = 0, Autocorrected = 1, IntegerNumber = 2, DecimalNumber = 3, Alphanumeric = 4, Name = 5, EmailAddress = 6, Password = 7, Pin = 8, Custom = 9 }
internal enum UnityInputType { Standard = 0, AutoCorrect = 1, Password = 2, Alphanumeric = 3, Name = 4, IntegerNumber = 5, DecimalNumber = 6, EmailAddress = 7, Pin = 8, Custom = 9 }
