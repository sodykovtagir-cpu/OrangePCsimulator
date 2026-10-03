"""Translations for every string of the 3DMork app.

Language order matches the ``*Short Form`` row of Assets/Resources/Translate.txt.
Placeholders written as ``{0}`` must stay untouched in every language - the
localization test compares them key by key.

Two helpers are used:

``add_map``    - every language spelled out, used for full sentences;
``add_token``  - one value reused for all languages that keep the same token,
                 with explicit overrides for the ones that translate it.
"""

LANGS = [
    "EN", "HU", "NL", "ES", "RO", "PL", "CS", "BS", "PT-BR", "PT-PT", "ET",
    "FR-CA", "FR-FR", "KR", "ZH-CN", "ZH-TW", "SR", "RU", "TR", "FI", "BG",
    "UA", "DE", "CAT", "IT", "JP", "LT", "AR", "KZ", "NO", "FIL", "HIN",
    "TH", "ID", "GE", "LV", "VN", "GR", "BRL", "SK", "SW", "FA",
]

STRINGS = {}


def _store(key, values):
    assert len(values) == len(LANGS), "%s: %d values, expected %d" % (key, len(values), len(LANGS))
    assert all(v.strip() for v in values), key
    assert key not in STRINGS, "duplicate key " + key
    STRINGS[key] = values


def add_map(key, mapping):
    missing = [lang for lang in LANGS if lang not in mapping]
    assert not missing, "%s: no translation for %s" % (key, missing)
    extra = [lang for lang in mapping if lang not in LANGS]
    assert not extra, "%s: unknown languages %s" % (key, extra)
    _store(key, [mapping[lang] for lang in LANGS])


def add_token(key, value, overrides=None):
    overrides = overrides or {}
    for lang in overrides:
        assert lang in LANGS, "%s: unknown language %s" % (key, lang)
    _store(key, [overrides.get(lang, value) for lang in LANGS])

# ---------------------------------------------------------------------------
# Start screen - buttons and table headers
# ---------------------------------------------------------------------------
add_token("3DMork start test", "START TEST", {
    "HU": "TESZT INDÍTÁSA", "NL": "TEST STARTEN", "ES": "INICIAR PRUEBA",
    "RO": "PORNESTE TESTUL", "PL": "URUCHOM TEST", "CS": "SPUSTIT TEST",
    "BS": "POKRENI TEST", "PT-BR": "INICIAR TESTE", "PT-PT": "INICIAR TESTE",
    "ET": "ALUSTA TEST", "FR-CA": "DÉMARRER LE TEST", "FR-FR": "DÉMARRER LE TEST",
    "KR": "테스트 시작", "ZH-CN": "开始测试", "ZH-TW": "開始測試", "SR": "ПОКРЕНИ ТЕСТ",
    "RU": "ЗАПУСТИТЬ ТЕСТ", "TR": "TESTİ BAŞLAT", "FI": "ALOITA TESTI",
    "BG": "СТАРТИРАЙ ТЕСТ", "UA": "ЗАПУСТИТИ ТЕСТ", "DE": "TEST STARTEN",
    "CAT": "INICIA LA PROVA", "IT": "AVVIA TEST", "JP": "テスト開始",
    "LT": "PRADĖTI TESTĄ", "AR": "ابدأ الاختبار", "KZ": "ТЕСТІ БАСТАУ",
    "NO": "START TEST", "FIL": "SIMULAN PANATILAHAN", "HIN": "परीक्षण शुरू करें",
    "TH": "เริ่มทดสอบ", "ID": "MULAI TES", "GE": "ტესტის დაწყება", "LV": "SĀKT TESTU",
    "VN": "BẮT ĐẦU KIỂM TRA", "GR": "Έναρξη δοκιμής", "BRL": "INICIAR TESTE",
    "SK": "SPUSTIŤ TEST", "SW": "ANZA TEST", "FA": "شروع تست",
})

add_token("3DMork to menu", "TO MENU", {
    "HU": "MENÜBE", "NL": "NAAR MENU", "ES": "AL MENÚ", "RO": "LA MENIU", "PL": "DO MENU",
    "CS": "DO MENU", "BS": "U MENU", "PT-BR": "PARA O MENU", "PT-PT": "PARA O MENU",
    "ET": "MENÜÜ", "FR-CA": "AU MENU", "FR-FR": "AU MENU", "KR": "메뉴로",
    "ZH-CN": "返回菜单", "ZH-TW": "返回選單", "SR": "U MENU", "RU": "В МЕНЮ",
    "TR": "MENÜYE", "FI": "VALIKKOON", "BG": "КЪМ МЕНЮТО", "UA": "ДО МЕНЮ",
    "DE": "ZUM MENÜ", "CAT": "AL MENÚ", "IT": "AL MENU", "JP": "メニューへ",
    "LT": "Į MENIU", "AR": "إلى القائمة", "KZ": "МЕНЮГЕ", "NO": "TIL MENY",
    "FIL": "PAPUNSA MENU", "HIN": "मेन्यू पर", "TH": "ไปที่เมนู", "ID": "KE MENU",
    "GE": "მენიუში", "LV": "UZ MENU", "VN": "VỀ MENU", "GR": "ΣΤΟ ΜΕΝΟΥ",
    "BRL": "PARA O MENU", "SK": "DO MENU", "SW": "KWENYE MENU", "FA": "به منو",
})

add_token("3DMork run again", "Run Again", {
    "HU": "Újrafuttatás", "NL": "Opnieuw", "ES": "Repetir", "RO": "Rulează din nou",
    "PL": "Uruchom ponownie", "CS": "Spustit znovu", "BS": "Pokreni ponovo",
    "PT-BR": "Executar novamente", "PT-PT": "Executar novamente", "ET": "Jooksuta uuesti",
    "FR-CA": "Relancer", "FR-FR": "Relancer", "KR": "다시 실행", "ZH-CN": "再次运行",
    "ZH-TW": "再次執行", "SR": "Ponovo pokreni", "RU": "ЕЩЁ РАЗ", "TR": "Tekrar Çalıştır",
    "FI": "Suorita uudelleen", "BG": "ПУСНИ ОТНОВО", "UA": "ЗАПУСТИТИ ЩЕ",
    "DE": "Erneut ausführen", "CAT": "Torna a executar", "IT": "Riesegui", "JP": "再実行",
    "LT": "Paleisti dar kartą", "AR": "تشغيل مرة أخرى", "KZ": "ҚАЙТА ІСКЕ ҚОСУ",
    "NO": "Kjør igjen", "FIL": "Muling patakbuhin", "HIN": "फिर से चलाएं",
    "TH": "เริ่มอีกครั้ง", "ID": "JALANKAN LAGI", "GE": "ხელახლა გაშვება",
    "LV": "PALAIST VĀL", "VN": "CHẠY LẠI", "GR": "Εκτέλεση ξανά",
    "BRL": "EXECUTAR NOVAMENTE", "SK": "Spustiť znova", "SW": "ENDELEZI TENA",
    "FA": "اجرای دوباره",
})

add_token("3DMork column pc", "PC", {
    "BS": "RAČUNALO", "ET": "ARVUTI", "ZH-CN": "电脑", "ZH-TW": "電腦",
    "SR": "RAČUNALO", "RU": "ПК", "UA": "ПК", "LT": "DARDAŠINIS", "AR": "حاسوب",
    "KZ": "КОМПЬЮТЕР", "HIN": "कंप्यूटर", "TH": "คอมพิวเตอร์", "GE": "კომპიუტერი",
    "LV": "DATORS", "VN": "MÁY TÍNH", "GR": "ΥΠΟΛΟΓΙΣΤΗΣ", "FA": "رایانه",
})

add_token("3DMork column cpu", "CPU", {
    "ZH-CN": "处理器", "ZH-TW": "處理器", "RU": "ЦП", "UA": "ЦП", "LT": "PROCESORIUS",
    "AR": "معالج", "KZ": "ПРОЦЕССОР", "HIN": "सीपीयू", "TH": "ซีพียู",
    "GE": "პროცესორი", "GR": "επεξεργαστής", "FA": "پردازنده",
})

add_token("3DMork column gpu", "GPU", {
    "KZ": "ВИДЕОКАРТА", "ZH-CN": "显卡", "ZH-TW": "顯示卡", "RU": "ВИДЕО", "UA": "ВІДЕО",
    "LT": "VAIZDO KORTELĖ", "AR": "كرت الشاشة", "HIN": "जीपीयू",
    "TH": "จีพียู", "GE": "ვიდეო", "GR": "κάρτα γραφικών", "FA": "کارت گرافیک",
})

add_token("3DMork column score", "SCORE", {
    "HU": "PONT", "ES": "PUNTOS", "RO": "PUNCTAJ", "PL": "WYNIK", "CS": "SKÓRE",
    "BS": "REZULTAT", "PT-BR": "PONTOS", "PT-PT": "PONTOS", "ET": "SKOOR", "KR": "점수",
    "ZH-CN": "得分", "ZH-TW": "得分", "SR": "REZULTAT", "RU": "СЧЁТ", "TR": "PUAN",
    "FI": "PISTEET", "BG": "ТОЧКИ", "UA": "БАЛИ", "DE": "PUNKTE", "CAT": "PUNTS",
    "IT": "PUNTI", "JP": "スコア", "LT": "TAŠKAI", "AR": "النتيجة", "KZ": "ҰПАЙ",
    "NO": "POENG", "FIL": "PUNT0", "HIN": "अंक", "TH": "คะแนน", "ID": "SKOR",
    "GE": "ქულა", "LV": "PUNKTI", "VN": "ĐIỂM", "GR": "ΒΑΘΜΟΣ", "BRL": "PONTOS",
    "SK": "SKÓRE", "SW": "ALAMA", "FA": "امتیاز",
})

add_token("3DMork column date", "DATE", {
    "HU": "DÁTUM", "NL": "DATUM", "ES": "FECHA", "RO": "DATĂ", "PL": "DATA",
    "CS": "DATUM", "BS": "DATUM", "PT-BR": "DATA", "PT-PT": "DATA", "ET": "KUUPÄEV",
    "KR": "날짜", "ZH-CN": "日期", "ZH-TW": "日期", "SR": "DATUM", "RU": "ДАТА",
    "TR": "TARİH", "FI": "PÄIVÄMÄÄRÄ", "BG": "ДАТА", "UA": "ДАТА", "DE": "DATUM",
    "CAT": "DATA", "IT": "DATA", "JP": "日付", "LT": "DATA", "AR": "التاريخ",
    "KZ": "КУНІ", "NO": "DATO", "FIL": "PETSA", "HIN": "तिथि", "TH": "วันที่",
    "ID": "TANGGAL", "GE": "თარიღი", "LV": "DATUMS", "VN": "NGÀY", "GR": "ΗΜΕΡΟΜΗΝΙΑ",
    "BRL": "DATA", "SK": "DÁTUM", "SW": "TAREHE", "FA": "تاریخ",
})

# ---------------------------------------------------------------------------
# Start screen - comparison table captions
# ---------------------------------------------------------------------------
add_map("3DMork comparison title", {
    "EN": "COMPARISON WITH OTHER PCs", "HU": "ÖSSZEHASONLÍTÁS MÁS PC-KVEL",
    "NL": "VERGELIJKING MET ANDERE PC'S", "ES": "COMPARACIÓN CON OTROS PC",
    "RO": "COMPARAȚIE CU ALTE PC-URI", "PL": "PORÓWNANIE Z INNYMI PC",
    "CS": "POROVNÁNÍ S JINÝMI PC", "BS": "UPOREDBA SA DRUGIM RAČUNARIMA",
    "PT-BR": "COMPARAÇÃO COM OUTROS PCs", "PT-PT": "COMPARAÇÃO COM OUTROS PC",
    "ET": "Võrdlus teiste arvutitega", "FR-CA": "COMPARAISON AVEC D'AUTRES PC",
    "FR-FR": "COMPARAISON AVEC D'AUTRES PC", "KR": "다른 PC와 비교",
    "ZH-CN": "与其他 PC 对比", "ZH-TW": "與其他 PC 比較",
    "SR": "UPOREDBA SA DRUGIM RAČUNARIMA", "RU": "СРАВНЕНИЕ С ДРУГИМИ ПК",
    "TR": "DİĞER PC'LERLE KARŞILAŞTIRMA", "FI": "VERTAILU MUIEN TIETOKONEIDEN",
    "BG": "СРАВНЕНИЕ С ДРУГИ ПК", "UA": "ПОРІВНЯННЯ З ІНШИМИ ПК",
    "DE": "VERGLEICH MIT ANDEREN PCs", "CAT": "COMPARACIÓ AMB ALTRES PC",
    "IT": "CONFRONTO CON ALTRI PC", "JP": "他のPCとの比較",
    "LT": "PALYGINIMAS SU KITOMIS PC", "AR": "مقارنة مع أجهزة كمبيوتر أخرى",
    "KZ": "БАСҚА КОМПЬЮТЕРЛЕРМЕН САЛЫСТЫРУ", "NO": "SAMMENLIGNING MED ANDRE PC-ER",
    "FIL": "PAGHAHALIP SA IBA PANG PC", "HIN": "अन्य पीसी से तुलना",
    "TH": "เปรียบเทียบกับเครื่องอื่น", "ID": "PERBANDINGAN DENGAN PC LAIN",
    "GE": "შედარება სხვა კომპიუტერებთან", "LV": "SALĪDZINĀJUMS AR CITIEM DATORIEM",
    "VN": "SO SÁNH VỚI CÁC PC KHÁC", "GR": "ΣΥΓΚΡΙΣΗ ΜΕ ΑΛΛΑ PC",
    "BRL": "COMPARAÇÃO COM OUTROS PCs", "SK": "POROVNANIE S INÝMI PC",
    "SW": "LINGANISHIA NA PC NYINGINE", "FA": "مقایسه با رایانه‌های دیگر",
})

add_map("3DMork comparison subtitle", {
    "EN": "Reference 3DMork results of the global leaderboard",
    "HU": "Értékelési 3DMork eredmények a globális ranglistáról",
    "NL": "Referentie-3DMork-resultaten van de wereldranglijst",
    "ES": "Resultados de referencia de 3DMork de la clasificación mundial",
    "RO": "Rezultate de referință 3DMork din clasamentul mondial",
    "PL": "Wyniki 3DMork z globalnej tabeli wyników",
    "CS": "Referenční výsledky 3DMork z globálního žebříčku",
    "BS": "Referentni rezultati 3DMork iz globalne rang-liste",
    "PT-BR": "Resultados de referência do 3DMork do ranking global",
    "PT-PT": "Resultados de referência do 3DMork do ranking global",
    "ET": "Globaalse edetabeli 3DMork tulemused",
    "FR-CA": "Résultats 3DMork de référence du classement mondial",
    "FR-FR": "Résultats 3DMork de référence du classement mondial",
    "KR": "글로벌 리더보드 기준 3DMork 결과",
    "ZH-CN": "全球排行榜 3DMork 参考成绩", "ZH-TW": "全球排行榜 3DMork 參考成績",
    "SR": "Referentni rezultati 3DMork iz globalne rang-liste",
    "RU": "Эталонные результаты 3DMork из общего рейтинга",
    "TR": "Genel sıralamadaki referans 3DMork sonuçları",
    "FI": "Maailman rankings tulokset 3DMorkista",
    "BG": "Референсни резултати 3DMork от световната класация",
    "UA": "Референсні результати 3DMork із загального рейтингу",
    "DE": "Referenz-3DMork-Ergebnisse der globalen Rangliste",
    "CAT": "Resultats de referència 3DMork de la classificació mundial",
    "IT": "Risultati di riferimento 3DMork della classifica mondiale",
    "JP": "世界ランキングの3DMorkリファレンス結果",
    "LT": "Pasaulio reitingo 3DMork rezultatai",
    "AR": "نتائج 3DMork المرجعية من الترتيب العالمي",
    "KZ": "Ғлобалдық рейтингтегі 3DMork анықтамалық нәтижелер",
    "NO": "Referanseresultater fra 3DMork i den globale ranglisten",
    "FIL": "Mga reference na resulta ng 3DMork mula sa pandaigdigang leaderboard",
    "HIN": "वैश्विक लीडरबोर्ड से 3DMork संदर्भ परिणाम",
    "TH": "ผลการอ้างอิง 3DMork จากอันดับโลก",
    "ID": "Hasil referensi 3DMork dari peringkat global",
    "GE": "მსოფლიო რეიტინგის 3DMork სარეფერნო შედეგები",
    "LV": "3DMork atsauces rezultāti no globālā saraksta",
    "VN": "Kết quả tham chiếu 3DMork từ bảng xếp hạng toàn cầu",
    "GR": "Αποτελέσματα αναφοράς 3DMork από τον παγκόσμιο πίνακα",
    "BRL": "Resultados de referência do 3DMork do ranking global",
    "SK": "Referenčné výsledky 3DMork z globálneho žebříčka",
    "SW": "Matokeo ya kumbukumbu ya 3DMork kutoka kwenye orodha ya dunia",
    "FA": "نتایج مرجع 3DMork از رتبه‌بندی جهانی",
})

add_map("3DMork comparison hint", {
    "EN": "Your PC is marked with * - the row updates after every run.",
    "HU": "A saját PC-d jele *, minden futás után frissül.",
    "NL": "Je eigen pc is gemarkeerd met * - de rij werkt na elke test bij.",
    "ES": "Tu PC está marcado con *: la fila se actualiza tras cada prueba.",
    "RO": "PC-ul tău este marcat cu * - rândul se actualizează după fiecare test.",
    "PL": "Twój PC jest oznaczony gwiazdką * - wiersz aktualizuje się po każdym teście.",
    "CS": "Tvůj počítač je označen hvězdičkou * - řádek se obnoví po každém testu.",
    "BS": "Tvoj računar je označen zvjezdicom * - red se osvježava nakon svakog testa.",
    "PT-BR": "O seu PC está marcado com * - a linha é atualizada após cada teste.",
    "PT-PT": "O seu PC está marcado com * - a linha é atualizada após cada teste.",
    "ET": "Sinu arvutis on tähistatud tärniga * - rida uuendub iga käivituse järel.",
    "FR-CA": "Ton PC est marqué d'un * - la ligne est mise à jour après chaque test.",
    "FR-FR": "Ton PC est marqué d'un * - la ligne est mise à jour après chaque test.",
    "KR": "내 PC는 *로 표시되며, 실행할 때마다 행이 갱신됩니다.",
    "ZH-CN": "你的电脑以 * 标记，每次测试后该行都会更新。",
    "ZH-TW": "你的電腦以 * 標記，每次測試後該列都會更新。",
    "SR": "Tvoj računar je označen zvjezdicom * - red se osvježava nakon svakog testa.",
    "RU": "Ваш ПК отмечен звёздочкой * — строка обновляется после каждого теста.",
    "TR": "Bilgisayarın yıldızla işaretlendi * - her çalıştırmadan sonra satır güncellenir.",
    "FI": "Tietokoneesi on merkitty tähdellä * - rivi päivittyy jokaisen ajon järel.",
    "BG": "Твоят компютър е отбелязан със * - редът се обновява след всеки тест.",
    "UA": "Ваш ПК позначено зіркою * - рядок оновлюється після кожного тесту.",
    "DE": "Dein PC ist mit * markiert - die Zeile wird nach jedem Lauf aktualisiert.",
    "CAT": "El teu PC està marcat amb * - la fila s'actualitza després de cada prova.",
    "IT": "Il tuo PC è contrassegnato con * - la riga si aggiorna dopo ogni test.",
    "JP": "あなたのPCは * で示されます。実行のたびにこの行が更新されます。",
    "LT": "Jūsų kompiuteris pažymėtas * - eilutė atnaujinama po kiekvieno testo.",
    "AR": "جهازك محدد بعلامة * - يتم تحديث الصف بعد كل تشغيل.",
    "KZ": "Компьютеріңіз * белгіменен - әр тесттен кейін жол жаңартылады.",
    "NO": "PC-en din er merket med * - raden oppdateres etter hvert kjør.",
    "FIL": "May PC mo ay may asterisko * - ina-update ang row pagkatapos ng bawat run.",
    "HIN": "आपका पीसी * से चिह्नित है - हर रन के बाद यह पंक्ति अपडेट होती है।",
    "TH": "พีซีของคุณถูกทำเครื่องหมาย * - แถวจะอัปเดตหลังจากการทดสอบแต่ละครั้ง",
    "ID": "PC Anda ditandai dengan * - baris diperbarui setelah setiap pengujian.",
    "GE": "თქვენი PC მონიშნულია * - სტრიქონი განახლდება ყოველი გაშვების შემდეგ.",
    "LV": "Jūsu dators ir atzīmēts ar * - rinda tiek atjaunināta pēc katra testa.",
    "VN": "PC của bạn được đánh dấu * - hàng được cập nhật sau mỗi lần chạy.",
    "GR": "Ο υπολογιστής σας σημειώνεται με * - η γραμμή ενημερώνεται μετά από κάθε εκτέλεση.",
    "BRL": "Seu PC está marcado com * - a linha é atualizada após cada teste.",
    "SK": "Váš počítač je označený hviezdičkou * - riadok sa aktualizuje po každom behu.",
    "SW": "PC yako limewekewa alama * - safu hii inasasishwa baada ya kilaendesho.",
    "FA": "رایانه شما با * مشخص شده است - پس از هر اجرا این ردیف به‌روزرسانی می‌شود.",
})

# ---------------------------------------------------------------------------
# Start screen - history panel
# ---------------------------------------------------------------------------
add_token("3DMork history title", "TEST HISTORY - THIS PC", {
    "HU": "TESZTELŐZMÉNYEK - EZ A PC", "NL": "TESTGESCHIEDENIS - DEZE PC",
    "ES": "HISTORIAL DE PRUEBAS - ESTE PC", "RO": "ISTORIC TESTE - ACEST PC",
    "PL": "HISTORIA TESTÓW - TEN PC", "CS": "HISTORIE TESTŮ - TENTO POČÍTAČ",
    "BS": "HISTORIJA TESTOVA - OVAJ RAČUNAR", "PT-BR": "HISTÓRICO DE TESTES - ESTE PC",
    "PT-PT": "HISTÓRICO DE TESTES - ESTE PC", "ET": "TESTIDE AJALUGU - SEE ARVUTI",
    "FR-CA": "HISTORIQUE DES TESTS - CE PC", "FR-FR": "HISTORIQUE DES TESTS - CE PC",
    "KR": "테스트 기록 - 이 PC", "ZH-CN": "测试历史 - 本机", "ZH-TW": "測試記錄 - 本機",
    "SR": "ISTORIJA TESTOVA - OVAJ RAČUNAR", "RU": "ИСТОРИЯ ТЕСТОВ - ЭТОТ ПК",
    "TR": "TEST GEÇMİŞİ - BU PC", "FI": "TESTIHISTORIA - TÄMÄ TIETOKONE",
    "BG": "ИСТОРИЯ НА ТЕСТОВЕТЕ - ТОЗИ КОМПЮТЪР",
    "UA": "ІСТОРІЯ ТЕСТІВ - ЦЕЙ ПК", "DE": "TESTVERLAUF - DIESER PC",
    "CAT": "HISTORIAL DE PROVES - AQUEST PC", "IT": "CRONOLOGIA DEI TEST - QUESTO PC",
    "JP": "テスト履歴 - このPC", "LT": "TESTŲ ISTORIJA - ŠIS KOMPIUTERIS",
    "AR": "سجل الاختبارات - هذا الحاسوب", "KZ": "ТЕСТТЕР ТАРИХЫ - ОСЫ КОМПЬЮТЕР",
    "NO": "TESTHISTORIKK - DENNE PC-EN", "FIL": "HISTORYA NG PAGSUBOK - ITONG PC",
    "HIN": "परीक्षण इतिहास - यह पीसी", "TH": "ประวัติการทดสอบ - เครื่องนี้",
    "ID": "RIWAYAT UJI - PC INI", "GE": "ტესტების ისტორია - ეს კომპიუტერი",
    "LV": "TESTU VĒSTURE - ŠIS DATORS", "VN": "LỊCH SỬ KIỂM TRA - PC NÀY",
    "GR": "Ιστορικό δοκιμών - Αυτό το PC", "BRL": "HISTÓRICO DE TESTES - ESTE PC",
    "SK": "HISTÓRIA TESTOV - TENTO POČÍTAČ", "SW": "HISTORIA YA Majaribio - PC HII",
    "FA": "تاریخچه آزمون‌ها - این رایانه",
})

add_map("3DMork history subtitle", {
    "EN": "Runs are stored locally on this computer",
    "HU": "A futások helyben, ezen a számítógépen tárolódnak",
    "NL": "De metingen worden lokaal op deze computer opgeslagen",
    "ES": "Las pruebas se guardan localmente en este equipo",
    "RO": "Rulările sunt stocate local pe acest calculator",
    "PL": "Wyniki są zapisywane lokalnie na tym komputerze",
    "CS": "Měření jsou uložena místně v tomto počítači",
    "BS": "Rezultati se čuvaju lokalno na ovom računaru",
    "PT-BR": "As execuções são armazenadas localmente neste computador",
    "PT-PT": "As execuções são guardadas localmente neste computador",
    "ET": "Katsed salvestatakse selles arvutis kohapeal",
    "FR-CA": "Les essais sont enregistrés localement sur cet ordinateur",
    "FR-FR": "Les essais sont enregistrés localement sur cet ordinateur",
    "KR": "실행 결과는 이 컴퓨터에 로컬로 저장됩니다",
    "ZH-CN": "测试记录保存在本机本地", "ZH-TW": "測試記錄儲存在本機本地",
    "SR": "Rezultati se čuvaju lokalno na ovom računaru",
    "RU": "Прогоны хранятся локально на этом компьютере",
    "TR": "Çalıştırmalar bu bilgisayarda yerel olarak saklanır",
    "FI": "Ajot tallennetaan tälle tietokoneelle paikallisesti",
    "BG": "Резултатите се пазят локално на този компютър",
    "UA": "Прогони зберігаються локально на цьому комп'ютері",
    "DE": "Die Läufe werden lokal auf diesem Rechner gespeichert",
    "CAT": "Les execucions es desen localment en aquest equip",
    "IT": "Le esecuzioni sono salvate localmente su questo computer",
    "JP": "計測結果はこのパソコンにローカル保存されます",
    "LT": "Rezultatai saugomi vietoje šiame kompiuteryje",
    "AR": "يتم حفظ نتائج التشغيل محليًا على هذا الحاسوب",
    "KZ": "Нәтижелер осы компьютерде жергілікті сақталады",
    "NO": "Kjøringene lagres lokalt på denne datamaskinen",
    "FIL": "Lokal na iniingatan ang mga pagtakbo sa computer na ito",
    "HIN": "रन इसी कंप्यूटर पर स्थानीय रूप से सहेजे जाते हैं",
    "TH": "ผลการทดสอบจะถูกเก็บไว้ในเครื่องนี้",
    "ID": "Hasil pengujian disimpan secara lokal di komputer ini",
    "GE": "გაშვებები ინახება ადგილოსნავდან ამ კომპიუტერზე",
    "LV": "Rezultāti tiek glabāti lokāli šajā datorā",
    "VN": "Kết quả được lưu cục bộ trên máy này",
    "GR": "Οι εκτελέσεις αποθηκεύονται τοπικά σε αυτόν τον υπολογιστή",
    "BRL": "As execuções são armazenadas localmente neste computador",
    "SK": "Behy sú uložené lokálne v tomto počítači",
    "SW": "Matokeo yakehifadhiwa ndani ya komputa hii",
    "FA": "اجراها به‌صورت محلی روی همین رایانه ذخیره می‌شود",
})

add_map("3DMork history empty", {
    "EN": "No benchmark runs on this PC yet.",
    "HU": "Ezen a PC-n még nincs egyetlen lefutott teszt sem.",
    "NL": "Nog geen benchmarktest op deze pc.",
    "ES": "Todavía no hay pruebas en este PC.",
    "RO": "Încă nu există niciun test pe acest PC.",
    "PL": "Na tym PC nie było jeszcze żadnego testu.",
    "CS": "Zatím na tomto počítači neproběhl žádný test.",
    "BS": "Još nema testova na ovom računaru.",
    "PT-BR": "Ainda não há testes neste PC.",
    "PT-PT": "Ainda não há testes neste PC.",
    "ET": "Selles arvutis pole veel ühtegi testi läbitud.",
    "FR-CA": "Aucun test n'a encore été lancé sur ce PC.",
    "FR-FR": "Aucun test n'a encore été lancé sur ce PC.",
    "KR": "이 PC에서는 아직 테스트를 실행하지 않았습니다.",
    "ZH-CN": "本机还没有运行过测试。", "ZH-TW": "本機還沒有執行過測試。",
    "SR": "Još nema testova na ovom računaru.",
    "RU": "На этом ПК тесты ещё не запускались.",
    "TR": "Bu bilgisayarda henüz test çalıştırılmadı.",
    "FI": "Tällä tietokoneella ei ole vielä ajettu testejä.",
    "BG": "На този компютър още няма изпълнени тестове.",
    "UA": "На цьому ПК ще не запускалися тести.",
    "DE": "Auf diesem PC wurde noch kein Test ausgeführt.",
    "CAT": "Encara no s'ha executat cap prova en aquest PC.",
    "IT": "Su questo PC non è ancora stato eseguito alcun test.",
    "JP": "このPCではまだテストを実行していません。",
    "LT": "Šiame kompiuteryje dar nebuvo paleista nė vieno testo.",
    "AR": "لم تُشغَّل أي اختبارات على هذا الحاسوب بعد.",
    "KZ": "Осы компьютерде әлі тест орындалмаған.",
    "NO": "Ingen tester er kjørt på denne PC-en ennå.",
    "FIL": "Wala pang nakikipag-ogn pagtakbo sa PC na ito.",
    "HIN": "इस पीसी पर अभी तक कोई परीक्षण नहीं चलाया गया।",
    "TH": "ยังไม่มีการทดสอบบนเครื่องนี้",
    "ID": "Belum ada pengujian di PC ini.",
    "GE": "ამ კომპიუტერზე ჯერ არ შესრულებილა ტესტი.",
    "LV": "Šajā datorā vēl nav izpildīts neviens tests.",
    "VN": "Chưa có lần chạy thử nào trên PC này.",
    "GR": "Δεν έχει εκτελεστεί καμία δοκιμή σε αυτό το PC.",
    "BRL": "Ainda não há testes neste PC.",
    "SK": "V tomto počítači ešte nebehol spustený žiadny test.",
    "SW": "Hakuna majaribio yaliyofanywa kwenye PC hii bado.",
    "FA": "هنوز هیچ آزمونی روی این رایانه اجرا نشده است.",
})

# ---------------------------------------------------------------------------
# Test and results screens
# ---------------------------------------------------------------------------
add_token("3DMork test title", "3DMork 3D Benchmark", {
    "HU": "3DMork 3D teszt", "NL": "3DMork 3D-benchmark", "ES": "Prueba 3D de 3DMork",
    "RO": "Test 3D 3DMork", "PL": "Test 3D 3DMork", "CS": "3D test 3DMork",
    "BS": "3DMork 3D test", "PT-BR": "Benchmark 3D do 3DMork", "PT-PT": "Benchmark 3D do 3DMork",
    "ET": "3DMork 3D test", "FR-CA": "Test 3D 3DMork", "FR-FR": "Test 3D 3DMork",
    "KR": "3DMork 3D 벤치마크", "ZH-CN": "3DMork 3D 测试", "ZH-TW": "3DMork 3D 測試",
    "SR": "3DMork 3D test", "RU": "3D-тест 3DMork", "TR": "3DMork 3D testi",
    "FI": "3DMork 3D -testi", "BG": "3D тест 3DMork", "UA": "3D-тест 3DMork",
    "DE": "3DMork 3D-Benchmark", "CAT": "Prova 3D de 3DMork", "IT": "Benchmark 3D 3DMork",
    "JP": "3DMork 3D ベンチマーク", "LT": "3DMork 3D testas", "AR": "اختبار 3D من 3DMork",
    "KZ": "3DMork 3D сынағы", "NO": "3DMork 3D-benchmark", "FIL": "3DMork 3D benchmark",
    "HIN": "3DMork 3D बेंचमार्क", "TH": "3DMork 3D เบนช์มาร์ก", "ID": "Benchmark 3D 3DMork",
    "GE": "3DMork 3D ტესტი", "LV": "3DMork 3D tests", "VN": "Kiểm tra 3D 3DMork",
    "GR": "Δοκιμή 3D 3DMork", "BRL": "Benchmark 3D do 3DMork", "SK": "3D test 3DMork",
    "SW": "Benchmark 3D ya 3DMork", "FA": "آزمون سه‌بعدی 3DMork",
})

add_token("3DMork results title", "3DMORK BENCHMARK RESULTS", {
    "HU": "3DMORK TESZTEREDMÉNYEK", "NL": "3DMORK-BENCHMARKRESULTATEN",
    "ES": "RESULTADOS DEL BENCHMARK 3DMORK", "RO": "REZULTATE BENCHMARK 3DMORK",
    "PL": "WYNIKI BENCHMARKU 3DMORK", "CS": "VÝSLEDKY TESTU 3DMORK",
    "BS": "REZULTATI 3DMORK BENCHMARKA", "PT-BR": "RESULTADOS DO BENCHMARK 3DMORK",
    "PT-PT": "RESULTADOS DO BENCHMARK 3DMORK", "ET": "3DMORKI TESTI TULEMUSED",
    "FR-CA": "RÉSULTATS DU TEST 3DMORK", "FR-FR": "RÉSULTATS DU TEST 3DMORK",
    "KR": "3DMORK 벤치마크 결과", "ZH-CN": "3DMORK 测试成绩", "ZH-TW": "3DMORK 測試成績",
    "SR": "REZULTATI 3DMORK BENCHMARKA", "RU": "РЕЗУЛЬТАТЫ ТЕСТА 3DMORK",
    "TR": "3DMORK BENCHMARK SONUÇLARI", "FI": "3DMORKIN TESTITULOKSET",
    "BG": "РЕЗУЛТАТИ ОТ 3DMORK ТЕСТА", "UA": "РЕЗУЛЬТАТИ ТЕСТУ 3DMORK",
    "DE": "3DMORK-BENCHMARK-ERGEBNISSE", "CAT": "RESULTATS DEL BENCHMARK 3DMORK",
    "IT": "RISULTATI BENCHMARK 3DMORK", "JP": "3DMORK ベンチマーク結果",
    "LT": "3DMORK TESTO REZULTATAI", "AR": "نتائج اختبار 3DMORK",
    "KZ": "3DMORK СЫНАҒЫ НӘТИЖЕЛЕРІ", "NO": "3DMORK-BENCHMARKRESULTATER",
    "FIL": "MGA RESULTA NG 3DMORK BENCHMARK", "HIN": "3DMORK बेंचमार्क परिणाम",
    "TH": "ผลการเบนช์มาร์ก 3DMORK", "ID": "HASIL BENCHMARK 3DMORK",
    "GE": "3DMORK ტესტის შედეგები", "LV": "3DMORK TESTA REZULTĀTI",
    "VN": "KẾT QUẢ KIỂM TRA 3DMORK", "GR": "ΑΠΟΤΕΛΕΣΜΑΤΑ ΔΟΚΙΜΗΣ 3DMORK",
    "BRL": "RESULTADOS DO BENCHMARK 3DMORK", "SK": "VÝSLEDKY TESTU 3DMORK",
    "SW": "MATOKEO YA BENCHMARK YA 3DMORK", "FA": "نتایج آزمون 3DMORK",
})

add_token("3DMork score label", "3DMORK SCORE", {
    "HU": "3DMORK PONT", "NL": "3DMORK-SCORE", "ES": "PUNTUACIÓN 3DMORK",
    "RO": "PUNCTAJ 3DMORK", "PL": "WYNIK 3DMORK", "CS": "SKÓRE 3DMORK",
    "BS": "REZULTAT 3DMORK", "PT-BR": "PONTUAÇÃO 3DMORK", "PT-PT": "PONTUAÇÃO 3DMORK",
    "ET": "3DMORKI SKOOR", "FR-CA": "SCORE 3DMORK", "FR-FR": "SCORE 3DMORK",
    "KR": "3DMORK 점수", "ZH-CN": "3DMORK 得分", "ZH-TW": "3DMORK 得分",
    "SR": "REZULTAT 3DMORK", "RU": "СЧЁТ 3DMORK", "TR": "3DMORK PUANI",
    "FI": "3DMORKIN PISTEET", "BG": "ТОЧКИ 3DMORK", "UA": "БАЛИ 3DMORK",
    "DE": "3DMORK-PUNKTE", "CAT": "PUNTUACIÓ 3DMORK", "IT": "PUNTEGGIO 3DMORK",
    "JP": "3DMORK スコア", "LT": "3DMORK TAŠKAI", "AR": "نتيجة 3DMORK",
    "KZ": "3DMORK ҰПАЙЫ", "NO": "3DMORK-SCORE", "FIL": "3DMORK SCORE",
    "HIN": "3DMORK अंक", "TH": "คะแนน 3DMORK", "ID": "SKOR 3DMORK",
    "GE": "3DMORK ქულა", "LV": "3DMORK PUNKTI", "VN": "ĐIỂM 3DMORK",
    "GR": "ΒΑΘΜΟΣ 3DMORK", "BRL": "PONTUAÇÃO 3DMORK", "SK": "SKÓRE 3DMORK",
    "SW": "ALAMA YA 3DMORK", "FA": "امتیاز 3DMORK",
})

add_token("3DMork graphics score", "Graphics Score", {
    "HU": "Grafikai pontszám", "NL": "Grafische score", "ES": "Puntuación gráfica",
    "RO": "Scor grafic", "PL": "Wynik graficzny", "CS": "Grafické skóre",
    "BS": "Grafički rezultat", "PT-BR": "Pontuação gráfica", "PT-PT": "Pontuação gráfica",
    "ET": "Graafiline skoor", "FR-CA": "Score graphique", "FR-FR": "Score graphique",
    "KR": "그래픽 점수", "ZH-CN": "显卡得分", "ZH-TW": "顯示卡得分",
    "SR": "Grafički rezultat", "RU": "Оценка графики", "TR": "Grafik puanı",
    "FI": "Grafiikkapisteet", "BG": "Графичен резултат", "UA": "Оцінка графіки",
    "DE": "Grafik-Punkte", "CAT": "Puntuació gràfica", "IT": "Punteggio grafico",
    "JP": "グラフィックスコア", "LT": "GRAFIKOS ĮVERČIAVIMAS", "AR": "درجة الرسوميات",
    "KZ": "ГРАФИКА НӘТИЖЕСІ", "NO": "Grafikkscore", "FIL": "Graphic score",
    "HIN": "ग्राफ़िक अंक", "TH": "คะแนนกราฟิก", "ID": "Skor grafis",
    "GE": "გრაფიკული შეფასება", "LV": "GRAFIKAS VĒRTĒJUMS", "VN": "Điểm đồ họa",
    "GR": "Βαθμολογία γραφικών", "BRL": "Pontuação gráfica", "SK": "Grafické skóre",
    "SW": "Alama ya grafiki", "FA": "امتیاز گرافیک",
})

add_token("3DMork physics score", "Physics / CPU Score", {
    "HU": "Fizika / CPU pontszám", "NL": "Fysica / CPU-score", "ES": "Puntuación física / CPU",
    "RO": "Scor fizică / CPU", "PL": "Wynik fizyki / CPU", "CS": "Skóre fyziky / CPU",
    "BS": "Fizika / CPU rezultat", "PT-BR": "Pontuação física / CPU",
    "PT-PT": "Pontuação física / CPU", "ET": "Füüsika / CPU skoor",
    "FR-CA": "Score physique / processeur", "FR-FR": "Score physique / processeur",
    "KR": "물리 / CPU 점수", "ZH-CN": "物理 / CPU 得分", "ZH-TW": "物理 / CPU 得分",
    "SR": "Fizika / CPU rezultat", "RU": "Физика / ЦП", "TR": "Fizik / CPU puanı",
    "FI": "Fysiikka / CPU-pisteet", "BG": "Резултат физика / CPU",
    "UA": "Оцінка фізики / ЦП", "DE": "Physik / CPU-Punkte", "CAT": "Puntuació física / CPU",
    "IT": "Punteggio fisica / CPU", "JP": "フィジックス / CPU スコア",
    "LT": "FIZIKOS / CPU ĮVERČIAVIMAS", "AR": "درجة الفيزياء / المعالج",
    "KZ": "ФИЗИКА / ПРОЦЕССОР", "NO": "Fysikk / CPU-score",
    "FIL": "Fisika / CPU score", "HIN": "फिजिक्स / सीपीयू अंक", "TH": "คะแนนฟิสิกส์ / ซีพียู",
    "ID": "Skor fisika / CPU", "GE": "ფიზიკული / CPU შეფასება", "LV": "FIZIKA / CPU VĒRTĒJUMS",
    "VN": "Điểm vật lý / CPU", "GR": "Βαθμολογία φυσικής / CPU", "BRL": "Pontuação física / CPU",
    "SK": "Skóre fyziky / CPU", "SW": "Alama ya fiziki / CPU", "FA": "امتیاز فیزیک / پردازنده",
})

add_token("3DMork memory score", "Memory Bandwidth", {
    "HU": "Memóriákvöz", "NL": "Geheugenbandbreedte", "ES": "Ancho de memoria",
    "RO": "Bandă de memorie", "PL": "Przepustowość RAM",
    "CS": "Šířka pásma paměti", "BS": "Širina pojasa RAM",
    "PT-BR": "Largura de banda RAM", "PT-PT": "Largura de banda RAM",
    "ET": "Mälu ribalus", "FR-CA": "Bande passante RAM", "FR-FR": "Bande passante RAM",
    "KR": "메모리 대역폭", "ZH-CN": "内存带宽", "ZH-TW": "記憶體頻寬",
    "SR": "Širina pojasa RAM", "RU": "Скорость ОЗУ",
    "TR": "Bellek bant genişliği", "FI": "Muistin kaista", "BG": "Скорост на паметта",
    "UA": "Швидкість ОЗП", "DE": "Speicherbandbreite", "CAT": "Ampleada de memòria",
    "IT": "Velocità memoria", "JP": "メモリ帯域", "LT": "ATMINTIES PLATŲMA",
    "AR": "نطاق الذاكرة", "KZ": "ЖАДЫЛЫҚ ӨТКІЗУ", "NO": "Minnebåndbredde",
    "FIL": "Lapisang ng memorya", "HIN": "मेमोरी बैंडविड्थ", "TH": "แบนด์วิดท์หน่วยความจำ",
    "ID": "Bandwidth memori", "GE": "მეხორის გამტარება", "LV": "ATMIŅAS PLATUMS",
    "VN": "Băng thông bộ nhớ", "GR": "Εύρος ζώνης μνήμης", "BRL": "Largura de banda RAM",
    "SK": "Šírka pásma pamäte", "SW": "Upana wa kumbukumbu", "FA": "پهنای باند حافظه",
})

add_token("3DMork fps score", "Average Frame Rate", {
    "HU": "Átlagos képkockaarány", "NL": "Gemiddelde beeldfrequentie",
    "ES": "Fotogramas por segundo medios", "RO": "Cadre mediu pe secundă",
    "PL": "Średnia liczba klatek", "CS": "Průměrný počet snímků",
    "BS": "Prosječna učestalost sličica", "PT-BR": "Taxa média de quadros",
    "PT-PT": "Taxa média de quadros", "ET": "Keskmine kaadrisagedus",
    "FR-CA": "Fréquence d'images moyenne", "FR-FR": "Fréquence d'images moyenne",
    "KR": "평균 프레임 레이트", "ZH-CN": "平均帧率", "ZH-TW": "平均影格率",
    "SR": "Prosječna učestalost sličica", "RU": "Средняя частота кадров",
    "TR": "Ortalama kare hızı", "FI": "Keskimääräinen ruudunpäivitys",
    "BG": "Средна честота на кадрите", "UA": "Середня частота кадрів",
    "DE": "Durchschnittliche Bildrate", "CAT": "Taxa mitjana de fotogrames",
    "IT": "Frequenza fotogrammi media", "JP": "平均フレームレート",
    "LT": "VIDUTINIO KADRO DAŽNIS", "AR": "معدل الإطارات المتوسط",
    "KZ": "ОРТАША КАДР ЖИІЛІГІ", "NO": "Gjennomsnittlig bildefrekvens",
    "FIL": "Karaniwang na frame rate", "HIN": "औसत फ़्रेम दर", "TH": "เฟรมเรตเฉลี่ย",
    "ID": "Rata bingkai rata-rata", "GE": "საშუალო კადრების სიხშივე", "LV": "VIDĒJĀ KADRU FREKVENCE",
    "VN": "Tốc độ khung hình trung bình", "GR": "Μέση ρυθμός καρέ",
    "BRL": "Taxa média de quadros", "SK": "Priemerná obnovovacia frekvencia",
    "SW": "Wastani wa kasi ya sura", "FA": "میانگین نرخ فریم",
})

# ---------------------------------------------------------------------------
# Values written by ThreeDMork.cs at runtime
# ---------------------------------------------------------------------------
add_map("3DMork hardware title", {
    "EN": "THIS PC - {0}", "HU": "EZ A PC - {0}", "NL": "DEZE PC - {0}",
    "ES": "ESTE PC - {0}", "RO": "ACEST PC - {0}", "PL": "TEN PC - {0}",
    "CS": "TENTO POČÍTAČ - {0}", "BS": "OVAJ RAČUNAR - {0}", "PT-BR": "ESTE PC - {0}",
    "PT-PT": "ESTE PC - {0}", "ET": "SEE ARVUTI - {0}", "FR-CA": "CE PC - {0}",
    "FR-FR": "CE PC - {0}", "KR": "이 PC - {0}", "ZH-CN": "本机 - {0}", "ZH-TW": "本機 - {0}",
    "SR": "OVAJ RAČUNAR - {0}", "RU": "ЭТОТ ПК — {0}", "TR": "BU PC - {0}",
    "FI": "TÄMÄ TIETOKONE - {0}", "BG": "ТОЗИ КОМПЮТЪР - {0}", "UA": "ЦЕЙ ПК — {0}",
    "DE": "DIESER PC - {0}", "CAT": "AQUEST PC - {0}", "IT": "QUESTO PC - {0}",
    "JP": "このPC - {0}", "LT": "ŠIS KOMPIUTERIS - {0}", "AR": "هذا الحاسوب - {0}",
    "KZ": "ОСЫ КОМПЬЮТЕР - {0}", "NO": "DENNE PC-EN - {0}", "FIL": "ITONG PC - {0}",
    "HIN": "यह पीसी - {0}", "TH": "เครื่องนี้ - {0}", "ID": "PC INI - {0}",
    "GE": "ეს კომპიუტერი - {0}", "LV": "ŠIS DATORS - {0}", "VN": "PC NÀY - {0}",
    "GR": "ΑΥΤΟ ΤΟ PC - {0}", "BRL": "ESTE PC - {0}", "SK": "TENTO POČÍTAČ - {0}",
    "SW": "PC HII - {0}", "FA": "این رایانه - {0}",
})

add_map("3DMork hardware no board", {
    "EN": "THIS PC - NO MOTHERBOARD", "HU": "EZ A PC - NINCS ALAPLAP",
    "NL": "DEZE PC - GEEN MOEDERBORD", "ES": "ESTE PC - SIN PLACA BASE",
    "RO": "ACEST PC - FĂRĂ PLACĂ DE BAZĂ", "PL": "TEN PC - BEZ PŁYTY GŁÓWNEJ",
    "CS": "TENTO POČÍTAČ - BEZ ZÁKLADNÍ DESKY", "BS": "OVAJ RAČUNAR - NEMA MATIČNE PLOČE",
    "PT-BR": "ESTE PC - SEM PLACA-MÃE", "PT-PT": "ESTE PC - SEM PLACA-MÃE",
    "ET": "SEE ARVUTI - EMAPLAADITA", "FR-CA": "CE PC - AUCUNE CARTE MÈRE",
    "FR-FR": "CE PC - AUCUNE CARTE MÈRE", "KR": "이 PC - 메인보드 없음",
    "ZH-CN": "本机 - 无主板", "ZH-TW": "本機 - 無主機板", "SR": "OVAJ RAČUNAR - NEMA MATIČNE PLOČE",
    "RU": "ЭТОТ ПК — НЕТ МАТЕРИНСКОЙ ПЛАТЫ", "TR": "BU PC - ANA KART YOK",
    "FI": "TÄLLÄ TIETOKONEELLA - EI EMOLEVYÄ", "BG": "ТОЗИ КОМПЮТЪР - НЯМА ДЪСКА",
    "UA": "ЦЕЙ ПК — НЕМА МАТЕРИНСЬКОЇ ПЛАТИ", "DE": "DIESER PC - KEIN MAINBOARD",
    "CAT": "AQUEST PC - SENSE PLAÇA BASE", "IT": "QUESTO PC - NESSUNA SCHEDA MADRE",
    "JP": "このPC - マザーボードなし", "LT": "ŠIAME KOMPIUTERYJE - NĖRA MOTININĖS PLATĖS",
    "AR": "هذا الحاسوب - لا توجد لوحة أم", "KZ": "ОСЫ КОМПЬЮТЕР - АНА ТАҚТА ЖОҚ",
    "NO": "DENNE PC-EN - INGEN HOVEDKORT", "FIL": "ITONG PC - WALANG MAINBOARD",
    "HIN": "यह पीसी - मदरबोर्ड नहीं", "TH": "เครื่องนี้ - ไม่มีเมนบอร์ด",
    "ID": "PC INI - TIDAK ADA MAINBOARD", "GE": "ეს კომპიუტერი - დედა დაფა არ არის",
    "LV": "ŠIS DATORS - NAV GALVENĀ PLATE", "VN": "PC NÀY - KHÔNG CÓ MAINBOARD",
    "GR": "ΑΥΤΟ ΤΟ PC - ΧΩΡΙΣ ΜΗΤΡΙΚΗ ΠΛΑΚΑ", "BRL": "ESTE PC - SEM PLACA-MÃE",
    "SK": "TENTO POČÍTAČ - BEZ ZÁKLADNEJ DOSKY", "SW": "PC HII - HAKUNA BODI",
    "FA": "این رایانه - بدون مادربرد",
})

add_map("3DMork hardware cpu", {
    "EN": "CPU: {0}", "HU": "CPU: {0}", "NL": "CPU: {0}", "ES": "CPU: {0}",
    "RO": "CPU: {0}", "PL": "CPU: {0}", "CS": "CPU: {0}", "BS": "CPU: {0}",
    "PT-BR": "CPU: {0}", "PT-PT": "CPU: {0}", "ET": "CPU: {0}", "FR-CA": "CPU : {0}",
    "FR-FR": "CPU : {0}", "KR": "CPU: {0}", "ZH-CN": "处理器：{0}", "ZH-TW": "處理器：{0}",
    "SR": "CPU: {0}", "RU": "ЦП: {0}", "TR": "CPU: {0}", "FI": "CPU: {0}",
    "BG": "CPU: {0}", "UA": "ЦП: {0}", "DE": "CPU: {0}", "CAT": "CPU: {0}", "IT": "CPU: {0}",
    "JP": "CPU: {0}", "LT": "CPU: {0}", "AR": "المعالج: {0}", "KZ": "ПРОЦЕССОР: {0}",
    "NO": "CPU: {0}", "FIL": "CPU: {0}", "HIN": "सीपीयू: {0}", "TH": "ซีพียู: {0}",
    "ID": "CPU: {0}", "GE": "CPU: {0}", "LV": "CPU: {0}", "VN": "CPU: {0}", "GR": "CPU: {0}",
    "BRL": "CPU: {0}", "SK": "CPU: {0}", "SW": "CPU: {0}", "FA": "پردازنده: {0}",
})

add_map("3DMork hardware gpu", {
    "EN": "GPU: {0}", "HU": "GPU: {0}", "NL": "GPU: {0}", "ES": "GPU: {0}",
    "RO": "GPU: {0}", "PL": "GPU: {0}", "CS": "GPU: {0}", "BS": "GPU: {0}",
    "PT-BR": "GPU: {0}", "PT-PT": "GPU: {0}", "ET": "GPU: {0}", "FR-CA": "GPU : {0}",
    "FR-FR": "GPU : {0}", "KR": "GPU: {0}", "ZH-CN": "显卡：{0}", "ZH-TW": "顯示卡：{0}",
    "SR": "GPU: {0}", "KZ": "ВИДЕОКАРТА: {0}", "RU": "ВИДЕО: {0}", "TR": "GPU: {0}", "FI": "GPU: {0}",
    "BG": "GPU: {0}", "UA": "ВІДЕО: {0}", "DE": "GPU: {0}", "CAT": "GPU: {0}", "IT": "GPU: {0}",
    "JP": "GPU: {0}", "LT": "GPU: {0}", "AR": "كرت الشاشة: {0}",
    "NO": "GPU: {0}", "FIL": "GPU: {0}", "HIN": "जीपीयू: {0}", "TH": "จีพียู: {0}",
    "ID": "GPU: {0}", "GE": "GPU: {0}", "LV": "GPU: {0}", "VN": "GPU: {0}", "GR": "GPU: {0}",
    "BRL": "GPU: {0}", "SK": "GPU: {0}", "SW": "GPU: {0}", "FA": "کارت گرافیک: {0}",
})

add_map("3DMork hardware ram", {
    "EN": "RAM: {0}", "HU": "RAM: {0}", "NL": "RAM: {0}", "ES": "RAM: {0}",
    "RO": "RAM: {0}", "PL": "RAM: {0}", "CS": "RAM: {0}", "BS": "RAM: {0}",
    "PT-BR": "RAM: {0}", "PT-PT": "RAM: {0}", "ET": "RAM: {0}", "FR-CA": "RAM : {0}",
    "FR-FR": "RAM : {0}", "KR": "메모리: {0}", "ZH-CN": "内存：{0}", "ZH-TW": "記憶體：{0}",
    "SR": "RAM: {0}", "RU": "ОЗУ: {0}", "TR": "RAM: {0}", "FI": "RAM: {0}",
    "BG": "RAM: {0}", "UA": "ОЗП: {0}", "DE": "RAM: {0}", "CAT": "RAM: {0}", "IT": "RAM: {0}",
    "JP": "RAM: {0}", "LT": "RAM: {0}", "AR": "الذاكرة: {0}", "KZ": "ЖАДЫЛЫҚ: {0}",
    "NO": "RAM: {0}", "FIL": "RAM: {0}", "HIN": "रैम: {0}", "TH": "แรม: {0}",
    "ID": "RAM: {0}", "GE": "RAM: {0}", "LV": "RAM: {0}", "VN": "RAM: {0}", "GR": "RAM: {0}",
    "BRL": "RAM: {0}", "SK": "RAM: {0}", "SW": "RAM: {0}", "FA": "حافظه: {0}",
})

add_map("3DMork hardware board", {
    "EN": "BOARD: {0}", "HU": "ALAPLAP: {0}", "NL": "MOEDERBORD: {0}", "ES": "PLACA BASE: {0}",
    "RO": "PLACĂ DE BAZĂ: {0}", "PL": "PŁYTA GŁÓWNA: {0}", "CS": "ZÁKLADNÍ DESKA: {0}",
    "BS": "MATIČNA PLOČA: {0}", "PT-BR": "PLACA-MÃE: {0}", "PT-PT": "PLACA-MÃE: {0}",
    "ET": "EMAPLAAT: {0}", "FR-CA": "CARTE MÈRE : {0}", "FR-FR": "CARTE MÈRE : {0}",
    "KR": "메인보드: {0}", "ZH-CN": "主板：{0}", "ZH-TW": "主機板：{0}", "SR": "MATIČNA PLOČA: {0}",
    "RU": "ПЛАТА: {0}", "TR": "ANA KART: {0}", "FI": "EMOLEVY: {0}", "BG": "ДЪСКА: {0}",
    "UA": "ПЛАТА: {0}", "DE": "MAINBOARD: {0}", "CAT": "PLACA BASE: {0}", "IT": "SCHEDA MADRE: {0}",
    "JP": "マザーボード：{0}",
    "LT": "MOTININĖ PLATĖ: {0}", "AR": "اللوحة الأم: {0}", "KZ": "ТАҚТА: {0}",
    "NO": "HOVEDKORT: {0}", "FIL": "MAINBOARD: {0}", "HIN": "मदरबोर्ड: {0}",
    "TH": "เมนบอร์ด: {0}", "ID": "MAINBOARD: {0}", "GE": "დედა დაფა: {0}",
    "LV": "GALVENĀ PLATE: {0}", "VN": "MAINBOARD: {0}", "GR": "ΜΗΤΡΙΚΗ ΠΛΑΚΑ: {0}",
    "BRL": "PLACA-MÃE: {0}", "SK": "ZÁKLADNÁ DOSKA: {0}", "SW": "BODI: {0}", "FA": "مادربرد: {0}",
})

add_map("3DMork hardware missing", {
    "EN": "not installed", "HU": "nincs telepítve", "NL": "niet geïnstalleerd",
    "ES": "no instalado", "RO": "neinstalat", "PL": "niezainstalowany",
    "CS": "nenainstalováno", "BS": "nije instalirano", "PT-BR": "não instalado",
    "PT-PT": "não instalado", "ET": "paigaldamata", "FR-CA": "non installé",
    "FR-FR": "non installé", "KR": "설치되지 않음", "ZH-CN": "未安装", "ZH-TW": "未安裝",
    "SR": "nije instalirano", "RU": "не установлен", "TR": "kurulu değil", "FI": "ei asennettu",
    "BG": "не е инсталиран", "UA": "не встановлено", "DE": "nicht installiert",
    "CAT": "no instal·lat", "IT": "non installato", "JP": "未インストール",
    "LT": "NEĮDIEGTA", "AR": "غير مثبت", "KZ": "ОРНАТЫЛМАҒАН", "NO": "ikke installert",
    "FIL": "hindi naka-install", "HIN": "स्थापित नहीं", "TH": "ยังไม่ได้ติดตั้ง",
    "ID": "belum terpasang", "GE": "დაუყენებელი", "LV": "NEINSTALĒTS",
    "VN": "chưa lắp đặt", "GR": "δεν είναι εγκατεστημένο", "BRL": "não instalado",
    "SK": "nenainštalované", "SW": "haijasakinishwa", "FA": "نصب نشده",
})

add_map("3DMork hardware build first", {
    "EN": "build a PC to run the benchmark", "HU": "szerelj össze egy PC-t a teszt futtatásához",
    "NL": "bouw een pc om de test te draaien", "ES": "monta un PC para ejecutar la prueba",
    "RO": "asamblează un PC pentru a rula testul", "PL": "złóż PC, aby uruchomić test",
    "CS": "sestav počítač pro spuštění testu", "BS": "sastavi računar da pokreneš test",
    "PT-BR": "monte um PC para rodar o teste", "PT-PT": "monta um PC para executar o teste",
    "ET": "ehita arvuti, et testi käivitada", "FR-CA": "construisez un PC pour lancer le test",
    "FR-FR": "construisez un PC pour lancer le test", "KR": "테스트를 실행하려면 PC를 조립하세요",
    "ZH-CN": "组装电脑后即可运行测试", "ZH-TW": "組裝電腦後即可執行測試",
    "SR": "sastavi računar da pokreneš test", "RU": "соберите ПК, чтобы запустить тест",
    "TR": "testi çalıştırmak için bir PC kur", "FI": "kokoa tietokone testin suorittamiseksi",
    "BG": "сглобете компютър, за да пуснете теста", "UA": "зберіть ПК, щоб запустити тест",
    "DE": "Baue einen PC, um den Test zu starten", "CAT": "munta un PC per executar la prova",
    "IT": "assembla un PC per eseguire il test", "JP": "テストを実行するにはPCを組んでください",
    "LT": "sudarykite kompiuterį, kad paleistumėte testą",
    "AR": "جمّع حاسوبًا لتشغيل الاختبار", "KZ": "сынақты іске қосу үшін компьютер жинаңыз",
    "NO": "bygg en PC for å kjøre testen", "FIL": "bumuo ng PC para patakbuhin ang benchmark",
    "HIN": "परीक्षण चलाने के लिए पीसी बनाएं", "TH": "ประกอบเครื่องเพื่อเริ่มทดสอบ",
    "ID": "rakit PC untuk menjalankan pengujian", "GE": "აშენეთ კომპიუტერი ტესტისთვის",
    "LV": "uzstādiet datoru, lai palaistu testu", "VN": "lắp ráp máy để chạy kiểm tra",
    "GR": "συναρμολογήστε έναν υπολογιστή για τη δοκιμή", "BRL": "monte um PC para rodar o teste",
    "SK": "zostroj počítač na spustenie testu", "SW": "jenga PC iliendeshe benchmark",
    "FA": "برای اجرای آزمون یک رایانه بسازید",
})

add_map("3DMork self pc", {
    "EN": "This PC", "HU": "Ez a PC", "NL": "Deze pc", "ES": "Este PC", "RO": "Acest PC",
    "PL": "Ten PC", "CS": "Tento počítač", "BS": "Ovaj računar", "PT-BR": "Este PC",
    "PT-PT": "Este PC", "ET": "See arvuti", "FR-CA": "Ce PC", "FR-FR": "Ce PC",
    "KR": "이 PC", "ZH-CN": "本机", "ZH-TW": "本機", "SR": "Ovaj računar", "RU": "Этот ПК",
    "TR": "Bu PC", "FI": "Tämä tietokone", "BG": "Този компютър", "UA": "Цей ПК",
    "DE": "Dieser PC", "CAT": "Aquest PC", "IT": "Questo PC", "JP": "このPC",
    "LT": "ŠIS KOMPIUTERIS", "AR": "هذا الحاسوب", "KZ": "ОСЫ КОМПЬЮТЕР", "NO": "DENNE PC-EN",
    "FIL": "ITONG PC", "HIN": "यह पीसी", "TH": "เครื่องนี้", "ID": "PC INI",
    "GE": "ეს კომპიუტერი", "LV": "ŠIS DATORS", "VN": "PC NÀY", "GR": "ΑΥΤΟ ΤΟ PC",
    "BRL": "ESTE PC", "SK": "TENTO POČÍTAČ", "SW": "PC HII", "FA": "این رایانه",
})

add_map("3DMork no cpu", {
    "EN": "no CPU", "HU": "nincs CPU", "NL": "geen cpu", "ES": "sin CPU", "RO": "fără CPU",
    "PL": "brak CPU", "CS": "bez CPU", "BS": "nema CPU", "PT-BR": "sem CPU",
    "PT-PT": "sem CPU", "ET": "CPU puudub", "FR-CA": "aucun processeur",
    "FR-FR": "aucun processeur", "KR": "CPU 없음", "ZH-CN": "无处理器", "ZH-TW": "無處理器",
    "SR": "nema CPU", "RU": "нет ЦП", "TR": "CPU yok", "FI": "ei CPU:ta",
    "BG": "няма CPU", "UA": "немає ЦП", "DE": "keine CPU", "CAT": "sense CPU",
    "IT": "nessuna CPU", "JP": "CPUなし", "LT": "nėra CPU", "AR": "لا يوجد معالج",
    "KZ": "процессор жоқ", "NO": "ingen CPU", "FIL": "walang CPU", "HIN": "कोई सीपीयू नहीं",
    "TH": "ไม่มีซีพียู", "ID": "tidak ada CPU", "GE": "CPU არ არის", "LV": "nav CPU",
    "VN": "không có CPU", "GR": "χωρίς επεξεργαστή", "BRL": "sem CPU", "SK": "bez CPU",
    "SW": "hakuna CPU", "FA": "بدون پردازنده",
})

add_map("3DMork no gpu", {
    "EN": "no GPU", "HU": "nincs GPU", "NL": "geen gpu", "ES": "sin GPU", "RO": "fără GPU",
    "PL": "brak GPU", "CS": "bez GPU", "BS": "nema GPU", "PT-BR": "sem GPU",
    "PT-PT": "sem GPU", "ET": "GPU puudub", "FR-CA": "aucun carte graphique",
    "FR-FR": "aucune carte graphique", "KR": "GPU 없음", "ZH-CN": "无显卡", "ZH-TW": "無顯示卡",
    "SR": "nema GPU", "RU": "нет видеокарты", "TR": "GPU yok", "FI": "ei GPU:ta",
    "BG": "няма GPU", "UA": "немає відеокарти", "DE": "keine GPU", "CAT": "sense GPU",
    "IT": "nessuna GPU", "JP": "GPUなし", "LT": "nėra GPU", "AR": "لا توجد بطاقة رسوميات",
    "KZ": "видеокарта жоқ", "NO": "ingen GPU", "FIL": "walang GPU", "HIN": "कोई जीपीयू नहीं",
    "TH": "ไม่มีจีพียู", "ID": "tidak ada GPU", "GE": "GPU არ არის", "LV": "nav GPU",
    "VN": "không có GPU", "GR": "χωρίς κάρτα γραφικών", "BRL": "sem GPU", "SK": "bez GPU",
    "SW": "hakuna GPU", "FA": "بدون کارت گرافیک",
})

add_map("3DMork average", {
    "EN": "Average of {0} reference PCs: {1}   |   Your best: {2}",
    "HU": "{0} referencia PC átlaga: {1}   |   A te legjobb: {2}",
    "NL": "Gemiddelde van {0} referentie-pc's: {1}   |   Jouw beste: {2}",
    "ES": "Media de {0} PC de referencia: {1}   |   Tu mejor: {2}",
    "RO": "Media a {0} PC-uri de referință: {1}   |   Cel mai bun al tău: {2}",
    "PL": "Średnia z {0} komputerów referencyjnych: {1}   |   Twój najlepszy: {2}",
    "CS": "Průměr {0} referenčních počítačů: {1}   |   Tvůj nejlepší: {2}",
    "BS": "Prosjek od {0} referentnih računara: {1}   |   Tvoj najbolji: {2}",
    "PT-BR": "Média de {0} PCs de referência: {1}   |   Seu melhor: {2}",
    "PT-PT": "Média de {0} PCs de referência: {1}   |   O seu melhor: {2}",
    "ET": "{0} võrdlusarvutis keskmine: {1}   |   Sinu parim: {2}",
    "FR-CA": "Moyenne de {0} PC de référence : {1}   |   Ton meilleur : {2}",
    "FR-FR": "Moyenne de {0} PC de référence : {1}   |   Ton meilleur : {2}",
    "KR": "기준 PC {0}대의 평균: {1}   |   내 최고 기록: {2}",
    "ZH-CN": "{0} 台参考电脑的平均分：{1}   |   你的最高分：{2}",
    "ZH-TW": "{0} 台參考電腦的平均分：{1}   |   你的最高分：{2}",
    "SR": "Prosjek od {0} referentnih računara: {1}   |   Tvoj najbolji: {2}",
    "RU": "Среднее по {0} эталонным ПК: {1}   |   Ваш лучший: {2}",
    "TR": "{0} referans PC ortalaması: {1}   |   En iyiniz: {2}",
    "FI": "{0} vertailutietokoneen keskiarvo: {1}   |   Parhaasi: {2}",
    "BG": "Средна стойност на {0} референтни компютъра: {1}   |   Най-доброто ви: {2}",
    "UA": "Середнє за {0} еталонними ПК: {1}   |   Ваш найкращий: {2}",
    "DE": "Durchschnitt von {0} Referenz-PCs: {1}   |   Dein Bestwert: {2}",
    "CAT": "Mitjana de {0} PC de referència: {1}   |   El teu millor: {2}",
    "IT": "Media di {0} PC di riferimento: {1}   |   Il tuo migliore: {2}",
    "JP": "基準PC {0} 台の平均：{1}   |   あなたのベスト：{2}",
    "LT": "{0} etaloninių kompiuterių vidurkis: {1}   |   Jūsų geriausias: {2}",
    "AR": "متوسط {0} حاسوب مرجعي: {1}   |   أفضل نتيجة لك: {2}",
    "KZ": "{0} анықтамалық компьютердің орташасы: {1}   |   Сіздің ең жақсысыңыз: {2}",
    "NO": "Gjennomsnitt av {0} referanse-PC-er: {1}   |   Din beste: {2}",
    "FIL": "Karaniwang ng {0} reference na PC: {1}   |   Iyong pinakamahusay: {2}",
    "HIN": "{0} संदर्भ पीसी का औसत: {1}   | आपका सर्वश्रेष्ठ: {2}",
    "TH": "ค่าเฉลี่ยของเครื่องอ้างอิง {0} เครื่อง: {1}   |   ผลดีที่สุดของคุณ: {2}",
    "ID": "Rata-rata {0} PC referensi: {1}   |   Skor terbaik Anda: {2}",
    "GE": "{0} სარეფერნო კომპიუტერის საშუალო: {1}   |   თქვენი საუკეთესო: {2}",
    "LV": "{0} atsauces datoru vidējais: {1}   |   Tavs labākais: {2}",
    "VN": "Trung bình {0} PC tham chiếu: {1}   |   Kết quả tốt nhất của bạn: {2}",
    "GR": "Μέσος όρος {0} διαθετικών PC: {1}   |   Το καλύτερό σου: {2}",
    "BRL": "Média de {0} PCs de referência: {1}   |   Seu melhor: {2}",
    "SK": "Priemer {0} referenčných počítačov: {1}   |   Tvoj najlepší: {2}",
    "SW": "Wastani wa PC za kumbukumbu {0}: {1}   |   Yako bora: {2}",
    "FA": "میانگین {0} رایانه مرجع: {1}   |   بهترین نتیجه شما: {2}",
})

add_token("3DMork best score", "Best score: {0}", {
    "HU": "Legjobb eredmény: {0}", "NL": "Beste score: {0}", "ES": "Mejor puntuación: {0}",
    "RO": "Cel mai bun scor: {0}", "PL": "Najlepszy wynik: {0}", "CS": "Nejlepší skóre: {0}",
    "BS": "Najbolji rezultat: {0}", "PT-BR": "Melhor pontuação: {0}",
    "PT-PT": "Melhor pontuação: {0}", "ET": "Parim tulemus: {0}", "FR-CA": "Meilleur score : {0}",
    "FR-FR": "Meilleur score : {0}", "KR": "최고 점수: {0}", "ZH-CN": "最高分：{0}",
    "ZH-TW": "最高分：{0}", "SR": "Najbolji rezultat: {0}", "RU": "Лучший результат: {0}",
    "TR": "En iyi puan: {0}", "FI": "Paras tulos: {0}", "BG": "Най-добър резултат: {0}",
    "UA": "Найкращий результат: {0}", "DE": "Beste Punktzahl: {0}", "CAT": "Millor puntuació: {0}",
    "IT": "Punteggio migliore: {0}", "JP": "最高スコア: {0}", "LT": "Geriausias rezultatas: {0}",
    "AR": "أفضل نتيجة: {0}", "KZ": "Ең жақсы нәтиже: {0}", "NO": "Beste score: {0}",
    "FIL": "Pinakamataas na score: {0}", "HIN": "सर्वश्रेष्ठ अंक: {0}", "TH": "คะแนนสูงสุด: {0}",
    "ID": "Skor terbaik: {0}", "GE": "საუკეთესო შედეგი: {0}", "LV": "Labākais rezultāts: {0}",
    "VN": "Điểm tốt nhất: {0}", "GR": "Καλύτερη βαθμολογία: {0}", "BRL": "Melhor pontuação: {0}",
    "SK": "Najlepšie skóre: {0}", "SW": "Alama bora: {0}", "FA": "بهترین امتیاز: {0}",
})

add_token("3DMork no runs yet", "-- (no runs yet)", {
    "HU": "-- (még nincs futás)", "NL": "-- (nog geen metingen)", "ES": "-- (sin pruebas aún)",
    "RO": "-- (încă niciun test)", "PL": "-- (jeszcze brak testów)",
    "CS": "-- (zatím žádný test)", "BS": "-- (još nema testova)", "PT-BR": "-- (sem testes ainda)",
    "PT-PT": "-- (sem testes ainda)", "ET": "-- (veel katseteta)", "FR-CA": "-- (aucun test)",
    "FR-FR": "-- (aucun test)", "KR": "-- (실행 기록 없음)", "ZH-CN": "--（尚无记录）",
    "ZH-TW": "--（尚無記錄）", "SR": "-- (još nema testova)", "RU": "-- (тестов ещё не было)",
    "TR": "-- (henüz test yok)", "FI": "-- (ei vielä ajoja)", "BG": "-- (няма изпълнени тестове)",
    "UA": "-- (тестів ще не було)", "DE": "-- (noch keine Läufe)", "CAT": "-- (cap prova)",
    "IT": "-- (nessun test)", "JP": "-- (未実行)", "LT": "-- (testų dar nebuvo)",
    "AR": "-- (لا توجد تشغيلات بعد)", "KZ": "-- (сынақтар әлі болмаған)",
    "NO": "-- (ingen kjøringer ennå)", "FIL": "-- (wala pang run)", "HIN": "-- (अभी कोई रन नहीं)",
    "TH": "-- (ยังไม่มีการทดสอบ)", "ID": "-- (belum ada pengujian)", "GE": "-- (ტესტები არ არის)",
    "LV": "-- (pārbaudes vēl nav)", "VN": "-- (chưa có lần chạy nào)", "GR": "-- (χωρίς εκτελέσεις)",
    "BRL": "-- (sem testes ainda)", "SK": "-- (zatiaľ bez behov)", "SW": "-- (bado hakunaendesho)",
    "FA": "-- (هنوز اجرایی نبوده)",
})

add_token("3DMork fps", "FPS: {0}", {
    "HU": "FPS: {0}", "NL": "FPS: {0}", "ES": "FPS: {0}", "RO": "FPS: {0}", "PL": "FPS: {0}",
    "CS": "FPS: {0}", "BS": "FPS: {0}", "PT-BR": "FPS: {0}", "PT-PT": "FPS: {0}", "ET": "FPS: {0}",
    "FR-CA": "IPS : {0}", "FR-FR": "IPS : {0}", "KR": "FPS: {0}", "ZH-CN": "帧率：{0}",
    "ZH-TW": "影格率：{0}", "SR": "FPS: {0}", "RU": "FPS: {0}", "TR": "FPS: {0}", "FI": "FPS: {0}",
    "BG": "FPS: {0}", "UA": "FPS: {0}", "DE": "FPS: {0}", "CAT": "FPS: {0}", "IT": "FPS: {0}",
    "JP": "FPS: {0}", "LT": "KADRAI/S: {0}", "AR": "إطار/ث: {0}", "KZ": "FPS: {0}",
    "NO": "FPS: {0}", "FIL": "FPS: {0}", "HIN": "FPS: {0}", "TH": "เฟรมเรต: {0}", "ID": "FPS: {0}",
    "GE": "FPS: {0}", "LV": "FPS: {0}", "VN": "FPS: {0}", "GR": "FPS: {0}", "BRL": "FPS: {0}",
    "SK": "FPS: {0}", "SW": "FPS: {0}", "FA": "فریم بر ثانیه: {0}",
})

add_map("3DMork initialising", {
    "EN": "Initializing 3DMork...", "HU": "3DMork inicializálása...", "NL": "3DMork initialiseren...",
    "ES": "Iniciando 3DMork...", "RO": "Se inițializează 3DMork...", "PL": "Inicjalizacja 3DMork...",
    "CS": "Inicializace 3DMork...", "BS": "Iniciranje 3DMork...", "PT-BR": "Iniciando 3DMork...",
    "PT-PT": "A iniciar 3DMork...", "ET": "3DMorki käivitamine...", "FR-CA": "Initialisation de 3DMork...",
    "FR-FR": "Initialisation de 3DMork...", "KR": "3DMork 초기화 중...", "ZH-CN": "正在初始化 3DMork...",
    "ZH-TW": "正在初始化 3DMork...", "SR": "Iniciranje 3DMork...", "RU": "Инициализация 3DMork...",
    "TR": "3DMork başlatılıyor...", "FI": "Alustetaan 3DMork...", "BG": "Инициализиране на 3DMork...",
    "UA": "Ініціалізація 3DMork...", "DE": "3DMork wird initialisiert...", "CAT": "S'inicialitza 3DMork...",
    "IT": "Inizializzazione di 3DMork...", "JP": "3DMork を初期化しています...", "LT": "Inicializuojamas 3DMork...",
    "AR": "جارٍ تهيئة 3DMork...", "KZ": "3DMork іске қосылуда...", "NO": "Starter 3DMork...",
    "FIL": "Sinisimula ang 3DMork...", "HIN": "3DMork प्रारंभ हो रहा है...", "TH": "กำลังเริ่มต้น 3DMork...",
    "ID": "Menginisialisasi 3DMork...", "GE": "იწყება 3DMork...", "LV": "Notiek 3DMork inicializēšana...",
    "VN": "Đang khởi tạo 3DMork...", "GR": "Αρχικοποίηση 3DMork...", "BRL": "Iniciando 3DMork...",
    "SK": "Inicializácia 3DMork...", "SW": "Inaanzisha 3DMork...", "FA": "در حال راه‌اندازی 3DMork...",
})

add_map("3DMork scene 1", {
    "EN": "Scene 1: Tech Showcase Flyby", "HU": "1. jelenet: technikai bemutató átrepés",
    "NL": "Scene 1: techshow-flyby", "ES": "Escena 1: vuelo de presentación",
    "RO": "Scena 1: survol de prezentare", "PL": "Scena 1: przelot prezentacyjny",
    "CS": "Scéna 1: přelet techniky", "BS": "Scena 1: prelet tehnike",
    "PT-BR": "Cena 1:Sobrevoo de apresentação", "PT-PT": "Cena 1: Sobrevoo de apresentação",
    "ET": "Stseen 1: tehnika tutvustus", "FR-CA": "Scène 1 : survol de présentation",
    "FR-FR": "Scène 1 : survol de présentation", "KR": "장면 1: 기술 쇼케이스 비행",
    "ZH-CN": "场景 1：硬件展示飞行", "ZH-TW": "場景 1：硬體展示飛行",
    "SR": "Scena 1: prelet tehnike", "RU": "Сцена 1: облёт техники", "TR": "Sahne 1: Tekni turu",
    "FI": "Kohtaus 1: tekniikan esittelyläento", "BG": "Сцена 1: обилакак на техниката",
    "UA": "Сцена 1: обліт техніки", "DE": "Szene 1: Technik-Flug", "CAT": "Escena 1: vol de mostra",
    "IT": "Scena 1: sorvolo di presentazione", "JP": "シーン1: 技術ショーケース",
    "LT": "1 scena: technologijos apžvalga", "AR": "المشهد 1: جولة استعراض", "KZ": "1-ші көрініс",
    "NO": "Scene 1: Teknik-flyby", "FIL": "Scene 1: Tech showcase flyby", "HIN": "दृश्य 1: तकनीक प्रदर्शन",
    "TH": "ฉาก 1: บินผ่านจุดแสดงเทคโนโลยี", "ID": "Adegan 1: flyby_nodejs teknologi",
    "GE": "სცენა 1: ტექნიკის ჩვენება", "LV": "1. aina: tehnikas apskats", "VN": "Cảnh 1: bay qua trưng bày",
    "GR": "Σκηνή 1: πτήση παρουσίασης", "BRL": "Cena 1: Sobrevoo de apresentação",
    "SK": "Scéna 1: prelet techniky", "SW": "Sura 1: mbio ya teknolojia", "FA": "صحنه ۱: پرواز معرفی",
})

add_map("3DMork scene 2", {
    "EN": "Scene 2: Hardware & Geometry Test", "HU": "2. jelenet: hardver- és geometriateszt",
    "NL": "Scene 2: hardware- en geometrietest", "ES": "Escena 2: prueba de hardware y geometría",
    "RO": "Scena 2: test hardware și geometrie", "PL": "Scena 2: test sprzętu i geometrii",
    "CS": "Scéna 2: test hardwaru a geometrie", "BS": "Scena 2: test hardvera i geometrije",
    "PT-BR": "Cena 2: teste de hardware e geometria", "PT-PT": "Cena 2: teste de hardware e geometria",
    "ET": "Stseen 2: riistvara ja geomeetria test", "FR-CA": "Scène 2 : test matériel et géométrie",
    "FR-FR": "Scène 2 : test matériel et géométrie", "KR": "장면 2: 하드웨어 및 지오메트리 테스트",
    "ZH-CN": "场景 2：硬件与几何测试", "ZH-TW": "場景 2：硬體與幾何測試",
    "SR": "Scena 2: test hardvera i geometrije", "RU": "Сцена 2: тест железа и геометрии",
    "TR": "Sahne 2: Donanım ve geometri testi", "FI": "Kohtaus 2: laitteisto- ja geometriatesti",
    "BG": "Сцена 2: тест на хардуера и геометрията", "UA": "Сцена 2: тест обладнання та геометрії",
    "DE": "Szene 2: Hardware- & Geometrietest", "CAT": "Escena 2: prova de maquinari i geometria",
    "IT": "Scena 2: test hardware e geometria", "JP": "シーン2: ハードウェアとジオメトリ",
    "LT": "2 scena: aparatūros ir geometrijos testas", "AR": "المشهد 2: اختبار العتاد والهندسة",
    "KZ": "2-ші көрініс: жабдық және геометрия сынағы", "NO": "Scene 2: Maskinvare- og geometritest",
    "FIL": "Scene 2: pagsubok ng hardware at geometri", "HIN": "दृश्य 2: हार्डवेयर और ज्यामिति परीक्षण",
    "TH": "ฉาก 2: ทดสอบฮาร์ดแวร์และเรขาคณิต", "ID": "Adegan 2: uji perangkat keras dan geometri",
    "GE": "სცენა 2: აპარატურისა და გეომეტრიის ტესტი", "LV": "2. aina: aparatūras un ģeometrijas tests",
    "VN": "Cảnh 2: kiểm tra phần cứng và hình học", "GR": "Σκηνή 2: δοκιμή υλικού και γεωμετρίας",
    "BRL": "Cena 2: teste de hardware e geometria", "SK": "Scéna 2: test hardvéru a geometrie",
    "SW": "Sura 2: mtihani wa vifaa na jiometria", "FA": "صحنه ۲: آزمون سخت‌افزار و هندسه",
})

add_map("3DMork scene 3", {
    "EN": "Scene 3: Dynamic Lighting Test", "HU": "3. jelenet: dinamikus megvilágítás teszt",
    "NL": "Scene 3: test met dynamische verlichting", "ES": "Escena 3: prueba de iluminación dinámica",
    "RO": "Scena 3: test iluminare dinamică", "PL": "Scena 3: test oświetlenia dynamicznego",
    "CS": "Scéna 3: test dynamického osvětlení", "BS": "Scena 3: test dinamičkog osvjetljenja",
    "PT-BR": "Cena 3: teste de iluminação dinâmica", "PT-PT": "Cena 3: teste de iluminação dinâmica",
    "ET": "Stseen 3: dünaamilise valgustuse test", "FR-CA": "Scène 3 : test d'éclairage dynamique",
    "FR-FR": "Scène 3 : test d'éclairage dynamique", "KR": "장면 3: 동적 조명 테스트",
    "ZH-CN": "场景 3：动态光照测试", "ZH-TW": "場景 3：動態光照測試",
    "SR": "Scena 3: test dinamičkog osvetljenja", "RU": "Сцена 3: тест динамического освещения",
    "TR": "Sahne 3: Dinamik aydınlatma testi", "FI": "Kohtaus 3: dynaamisen valaistuksen testi",
    "BG": "Сцена 3: тест на динамично осветление", "UA": "Сцена 3: тест динамічного освітлення",
    "DE": "Szene 3: Dynamische Beleuchtung", "CAT": "Escena 3: prova d'il·luminació dinàmica",
    "IT": "Scena 3: test illuminazione dinamica", "JP": "シーン3: ダイナミックライティング",
    "LT": "3 scena: dinaminio apšvietimo testas", "AR": "المشهد 3: اختبار الإضاءة الديناميكية",
    "KZ": "3-ші көрініс: динамикалық жарықтандыру сынағы", "NO": "Scene 3: Dynamisk belysning",
    "FIL": "Scene 3: pagsubok sa dynamic na ilaw", "HIN": "दृश्य 3: गतिशील प्रकाश परीक्षण",
    "TH": "ฉาก 3: ทดสอบแสงแบบไดนามิก", "ID": "Adegan 3: uji pencahayaan dinamis",
    "GE": "სცენა 3: დინამიური განათების ტესტი", "LV": "3. aina: dinamiskas apgaismojuma tests",
    "VN": "Cảnh 3: kiểm tra ánh sáng động", "GR": "Σκηνή 3: δοκιμή δυναμικού φωτισμού",
    "BRL": "Cena 3: teste de iluminação dinâmica", "SK": "Scéna 3: test dynamického osvetlenia",
    "SW": "Sura 3: mtihani wa mwangaza wa kwao", "FA": "صحنه ۳: آزمون نورپردازی پویا",
})

add_token("3DMork column fps", "FPS", {})

add_map("3DMork scene generic", {
    "EN": "Scene {0}", "HU": "{0}. jelenet", "NL": "Scène {0}", "ES": "Escena {0}",
    "RO": "Scena {0}", "PL": "Scena {0}", "CS": "Scéna {0}", "BS": "Scena {0}",
    "PT-BR": "Cena {0}", "PT-PT": "Cena {0}", "ET": "Stseen {0}", "FR-CA": "Scène {0}",
    "FR-FR": "Scène {0}", "KR": "장면 {0}", "ZH-CN": "场景 {0}", "ZH-TW": "場景 {0}",
    "SR": "Scena {0}", "RU": "Сцена {0}", "TR": "Sahne {0}", "FI": "Kohtaus {0}",
    "BG": "Сцена {0}", "UA": "Сцена {0}", "DE": "Szene {0}", "CAT": "Escena {0}",
    "IT": "Scena {0}", "JP": "シーン{0}", "LT": "{0} scena", "AR": "مشهد {0}",
    "KZ": "{0}-ші көрініс", "NO": "Scene {0}", "FIL": "Scene {0}", "HIN": "दृश्य {0}",
    "TH": "ฉาก {0}", "ID": "Adegan {0}", "GE": "სცენა {0}", "LV": "{0}. aina",
    "VN": "Cảnh {0}", "GR": "Σκηνή {0}", "BRL": "Cena {0}", "SK": "Scéna {0}",
    "SW": "Sura {0}", "FA": "صحنه {0}",
})
