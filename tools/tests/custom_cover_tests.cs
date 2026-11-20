// Contracts for «кастомная крышка приходит в коробке» and «пароль Wi-Fi
// скрыт звёздочками». Код компилируется вместе с настоящими Box.cs и
// CustomPaint.cs — движок и Main подменены заглушками.
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

    private static void TestCustomCoverInBox()
    {
        Console.WriteLine("[1] Заказ кастомной крышки -> коробка -> предмет с рисунком");
        var m = GameMain.Create();
        Box.Restoring = false;
        Box.CustomDelivery = (Func<GameObject>)null;
        int start = UnityEngine.Object.InstantiateCount;

        var boxPrefab = NewBoxPrefab();
        var itemPrefab = NewCoverPrefab("cover_atx_custom");
        var texture = new Texture2D(128, 128);

        bool painted = false;
        bool ok = m.DeliverCustom(itemPrefab, boxPrefab, go =>
        {
            var paint = go.GetComponent<CustomPaint>();
            if (paint == null) return;
            paint.SetTexture(texture, new byte[] { 7 });
            painted = true;
        });

        Check(ok, "заказ принят");
        Check(m.Queue.Count == 1 && m.Queue[0] == boxPrefab, "в доставку ушла коробка, а не голая крышка");
        Check(UnityEngine.Object.InstantiateCount == start, "крышка не появляется сразу, без коробки");
        Check(!painted, "рисунок не наносится в момент заказа");

        // Игрок получил посылку: доставка создаёт коробку, Start() видит отложенный режим.
        var live = m.SpawnFromQueue(boxPrefab);
        var box = live.GetComponent<Box>();
        BoxTestHooks.Start(box);
        Check(painted, "рисунок нанесён при появлении коробки (Box.Deferred -> Release)");
        Check(UnityEngine.Object.InstantiateCount == start + 2, "созданы ровно два объекта: посылка и крышка внутри");
        Check(!box.Pending, "коробка выложила предмет, а не осталась ждать");
        Check(BoxTestHooks.CustomDeliveryWithoutPending(), "очередь кастомных заказов опустела");
    }

    private static void TestTwoOrdersKeepTheirPaint()
    {
        Console.WriteLine("[2] Два заказа подряд не путают рисунки");
        var m = GameMain.Create();
        Box.Restoring = false;
        Box.CustomDelivery = (Func<GameObject>)null;
        var boxPrefab = NewBoxPrefab();
        var first = NewCoverPrefab("cover_atx_custom");
        var second = NewCoverPrefab("cover_itx_custom");
        string paintedFor = null;

        m.DeliverCustom(first, boxPrefab, go => paintedFor = "A:" + (go.name ?? ""));
        m.DeliverCustom(second, boxPrefab, go => paintedFor = "B:" + (go.name ?? ""));

        Check(m.Queue.Count == 2, "обе коробки в очереди доставки");
        var boxA = m.SpawnFromQueue(boxPrefab);
        BoxTestHooks.Start(boxA.GetComponent<Box>());
        Check(paintedFor == "A:cover_atx_custom_copy", "первая коробка получила первую крышку");
        var boxB = m.SpawnFromQueue(boxPrefab);
        BoxTestHooks.Start(boxB.GetComponent<Box>());
        Check(paintedFor == "B:cover_itx_custom_copy", "вторая коробка получила вторую крышку");
    }

    private static void TestRestoringSilence()
    {
        Console.WriteLine("[3] Загрузка сейва не плодит дубликаты");
        var m = GameMain.Create();
        Box.Restoring = true;
        Box.CustomDelivery = (Func<GameObject>)null;
        var boxPrefab = NewBoxPrefab();
        m.DeliverCustom(NewCoverPrefab("cover_atx_custom"), boxPrefab, go => { });
        int before = UnityEngine.Object.InstantiateCount;
        var live = m.SpawnFromQueue(boxPrefab);
        BoxTestHooks.Start(live.GetComponent<Box>());
        Check(UnityEngine.Object.InstantiateCount == before + 1, "во время Restoring создана только сама коробка");
        Box.Restoring = false;

        // Обычная (не кастомная) коробка ведёт себя как раньше.
        var plainItem = new GameObject("cpu");
        var plain = new GameObject("Box_CPU").AddComponent<Box>();
        BoxTestHooks.SetPrefab(plain, plainItem);
        int n = UnityEngine.Object.InstantiateCount;
        BoxTestHooks.Start(plain);
        Check(UnityEngine.Object.InstantiateCount == n + 1, "обычная коробка выкладывает содержимое");
        plain.Open();
        Check(UnityEngine.Object.InstantiateCount == n + 1, "повторный Open() не плодит копии");
    }

    private static void TestModForgeWiring()
    {
        Console.WriteLine("[4] ModForge: покупка -> коробка");
        var src = Read("Assets/Scripts/Assembly-CSharp/PC/Component/Software/ModForge.cs");
        Check(src.Contains("Main.Instance.DeliverCustom("), "покупка идёт через DeliverCustom");
        Check(src.Contains("Box_CustomCover") && src.Contains("Resources.Load<GameObject>"),
              "коробка крышки грузится из Resources/Box_CustomCover");
        Check(src.Contains("private Box caseBoxPrefab"), "поле коробки можно назначить в инспекторе");
        Check(src.Contains("m.AddMoney(price)"), "неудачная доставка возвращает деньги");
        Check(!src.Contains("InstantDelivery"), "мгновенной выдачи крышки больше нет ни в одном пути");
    }

    private static void TestWifiMasked()
    {
        Console.WriteLine("[5] Пароль Wi-Fi — звёздочки");
        var entry = Read("Assets/Scripts/Assembly-CSharp/PC/Component/Software/OS/WifiNetworkEntry.cs");
        Check(entry.Contains("private void PreparePasswordField()"), "единая точка настройки поля пароля");
        Check(entry.Contains("password.contentType = InputField.ContentType.Password"), "тип Password ставится в рантайме");
        Check(entry.Contains("password.asteriskChar = '*'"), "символ маски задан явно");
        Check(entry.Contains("private void Awake()"), "маска включена ещё до Initialize");
        var prefab = Read("Assets/Scripts/Assembly-CSharp/PC/Component/WifiPanelPrefab.prefab");
        Check(prefab.Contains("m_ContentType: 7") && prefab.Contains("m_InputType: 2"),
              "в префабе поле пароля — ContentType.Password (7), InputType.Password (2)");
        Check(!prefab.Contains("m_ContentType: 0"), "в префабе не осталось Standard-поля");
        // Unity: ContentType.Password = 7, InputType.Standard = 1.
        // InputType.Password(2)/Pin(8) — это PIN-поле по центру, маску не рисует.
        Check((int)UnityInputType.Password == 2 && (int)UnityInputType.Pin == 8,
              "значения InputType как в Unity");
        Check((int)UnityContentType.Password == 7, "ContentType.Password == 7");
    }

    private static void TestRouterSiteFields()
    {
        Console.WriteLine("[6] Сайт роутера тоже не показывает пароль");
        var pages = Read("Assets/Scripts/Assembly-CSharp/PC/Component/Software/RouterWebPages.cs");
        Check(pages.Contains("private static void Secure(InputField input)"), "общий помощник Secure()");
        Check(pages.Contains("input.asteriskChar = '*'"), "Secure() ставит маску");
        Check(Count(pages, "Secure(key)") == 1 && Count(pages, "Secure(parentKey)") == 1,
              "оба поля пароля пропущены через Secure()");
        var secureBody = pages.Substring(pages.IndexOf("private static void Secure(InputField input)",
            StringComparison.Ordinal));
        secureBody = secureBody.Substring(0, secureBody.IndexOf('}'));
        Check(secureBody.Contains("contentType = InputField.ContentType.Password") &&
              secureBody.Contains("asteriskChar = '*'"), "маска ставится только через Secure()");
    }

    private static void TestAssets()
    {
        Console.WriteLine("[7] Ассеты");
        var prefab = Read("Assets/GameObject/Box_CustomCover.prefab");
        Check(prefab.Contains("m_Name: Box_CustomCover"), "префаб коробки существует");
        Check(prefab.Contains("m_TagString: Box"), "тег Box — работают Trash Bag и бомба-уборщик");
        Check(prefab.Contains("prefab: {fileID: 0}"), "содержимое подставляет код, а не ассет");
        Check(!prefab.Contains("8f4686ada7e9ecb498d6d02b15a689de"), "нет ссылки на чужую фигурку");
        Check(prefab.Contains("HingeJoint"), "коробка открывается как остальные");
        var meta = Read("Assets/GameObject/Box_CustomCover.prefab.meta");
        Check(meta.Contains("guid: 45f4ee7830e9463889927add6b7b4dd9"), "guid префаба стабилен");
        var modforge = Read("Assets/GameObject/ModForge.prefab");
        Check(modforge.Contains("caseBoxPrefab"), "ссылка на коробку сериализована в префабе ModForge");
        var main = Read("Assets/Scripts/Assembly-CSharp/Main.cs");
        Check(main.Contains("public event Action<GameObject> Spawned"), "событие появления доставки объявлено");
        Check(main.Contains("NotifySpawned(Instantiate(original, packagePoint.position"), "очередь доставки сообщает о появлении");
        Check(main.Contains("NotifySpawned(Instantiate(prefab, portalPoint.position"), "мгновенная доставка сообщает о появлении");
        Check(main.Contains("Box.Restoring") && main.Contains("if (Box.Restoring)"), "загрузка сейва учтена в доставке");
    }

    // Префаб крышки: Renderer + CustomPaint, как у cover_atx_custom.prefab.
    private static GameObject NewCoverPrefab(string name)
    {
        var go = new GameObject(name);
        go.AddComponent<Renderer>();
        go.AddComponent<CustomPaint>();
        return go;
    }

    // Префаб коробки: так же, как Box_CustomCover.prefab, несёт компонент Box.
    private static GameObject NewBoxPrefab()
    {
        var go = new GameObject("Box_CustomCover");
        go.AddComponent<Box>();
        return go;
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
        TestCustomCoverInBox();
        TestTwoOrdersKeepTheirPaint();
        TestRestoringSilence();
        TestModForgeWiring();
        TestWifiMasked();
        TestRouterSiteFields();
        TestAssets();
        Console.WriteLine(failures == 0
            ? "PASS: custom cover arrives in a box; Wi-Fi password is masked everywhere"
            : "FAILED: " + failures);
        return failures == 0 ? 0 : 1;
    }
}

// Unity enum values the fix depends on (compile-time constants).
internal enum UnityContentType { Standard = 0, Autocorrected = 1, IntegerNumber = 2, DecimalNumber = 3, Alphanumeric = 4, Name = 5, EmailAddress = 6, Password = 7, Pin = 8, Custom = 9 }
internal enum UnityInputType { Standard = 0, AutoCorrect = 1, Password = 2, Alphanumeric = 3, Name = 4, IntegerNumber = 5, DecimalNumber = 6, EmailAddress = 7, Pin = 8, Custom = 9 }
