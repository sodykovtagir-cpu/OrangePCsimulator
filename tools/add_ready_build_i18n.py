#!/usr/bin/env python3
"""
add_ready_build_i18n.py — строки локализации для готовых ПК и майнеров.

Translate.txt — TSV: первая колонка ключ, дальше 42 языка. tools/test_localization.py
требует, чтобы КАЖДЫЙ ключ был заполнен на всех 42 языках, поэтому здесь лежат
полные переводы, а не только EN/RU.

Описания товаров намеренно сделаны спецификациями («i5-8400 / 16 GB / GTX 1060 /
512 GB SSD + 1 TB HDD»): названия железа не переводятся, строка одинаково читается
на любом языке и не протухнет при смене комплектующих.

Идемпотентно: существующие ключи обновляются, новые дописываются в конец.
"""

from __future__ import annotations

import os
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PATH = os.path.join(REPO, "Assets/Resources/Translate.txt")

LANGS = [
    "EN", "HU", "NL", "ES", "RO", "PL", "CS", "BS", "PT-BR", "PT-PT", "ET",
    "FR-CA", "FR-FR", "KR", "ZH-CN", "ZH-TW", "SR", "RU", "TR", "FI", "BG",
    "UA", "DE", "CAT", "IT", "JP", "LT", "AR", "KZ", "NO", "FIL", "HIN", "TH",
    "ID", "GE", "LV", "VN", "GR", "BRL", "SK", "SW", "FA",
]

# Названия страниц и товаров — переводы по всем 42 языкам.
NAMES = {
    "Ready PC": [
        "Ready PC", "Kész PC", "Kant-en-klare pc", "PC listos", "PC-uri gata",
        "Gotowe PC", "Hotové PC", "Gotovi PC", "PCs prontos", "PCs prontos",
        "Valmis arvuti", "PC prêts", "PC prêts", "완제품 PC", "整机", "整機",
        "Готови рачунари", "Готовые ПК", "Hazır PC", "Valmiit tietokoneet",
        "Готови компютри", "Готові ПК", "Fertig-PCs", "PC llestos", "PC pronti",
        "完成品PC", "Paruošti kompiuteriai", "حواسيب جاهزة", "Дайын ДК",
        "Ferdige PC-er", "Handang PC", "तैयार पीसी", "พีซีสำเร็จรูป", "PC siap pakai",
        "მზა კომპიუტერები", "Gatavi datori", "PC dựng sẵn", "Έτοιμα PC",
        "Гатовыя ПК", "Hotové PC", "Färdiga datorer", "رایانه‌های آماده",
    ],
    "Ready Miner": [
        "Ready Miner", "Kész bányász", "Kant-en-klare miner", "Mineros listos",
        "Minere gata", "Gotowe koparki", "Hotové těžební stroje",
        "Gotovi rudari", "Mineradoras prontas", "Mineradoras prontas",
        "Valmis kaevur", "Mineurs prêts", "Mineurs prêts", "완제품 채굴기",
        "整机矿机", "整機礦機", "Готови рудари", "Готовые майнеры",
        "Hazır madenci", "Valmiit louhijat", "Готови копачи", "Готові майнери",
        "Fertige Miner", "Miners llestos", "Miner pronti", "完成品マイナー",
        "Paruošti kasėjai", "معدّنات جاهزة", "Дайын майнерлер",
        "Ferdige minere", "Handang miner", "तैयार माइनर", "เครื่องขุดสำเร็จรูป",
        "Penambang siap pakai", "მზა მაინერები", "Gatavi mainari",
        "Máy đào dựng sẵn", "Έτοιμοι miner", "Гатовыя майнеры",
        "Hotové ťažobné stroje", "Färdiga miners", "ماینرهای آماده",
    ],
    # Название-прозвище вместо сухого "Office PC" — выбран вариант A.
    # По языкам подобран смысловой аналог "рабочей лошадки", а не калька:
    # там, где такого разговорного слова нет, берётся ближайшее по духу
    # ("труженик", "работник", "вьючная лошадка").
    # Выбор пользователя: прилагательное без слова "ПК" — в магазине к нему
    # дописывается цвет корпуса, получается "Офисный (Чёрный)".
    "Office PC": [
        "Office", "Irodai", "Kantoor", "De oficina", "De birou", "Biurowy",
        "Kancelářský", "Kancelarijski", "De escritório", "De escritório",
        "Kontori", "De bureau", "De bureau", "사무용", "办公", "辦公",
        "Канцеларијски", "Офисный", "Ofis", "Toimisto", "Офисен", "Офісний",
        "Büro", "D'oficina", "Da ufficio", "オフィス", "Biuro", "مكتبي",
        "Кеңсе", "Kontor", "Pang-opisina", "ऑफिस", "สำนักงาน", "Kantor",
        "საოფისე", "Biroja", "Văn phòng", "Γραφείου", "Офісны",
        "Kancelársky", "Kontor", "اداری",
    ],
    "Home PC": [
        "Home", "Otthoni", "Thuis", "Doméstico", "De acasă", "Domowy",
        "Domácí", "Kućni", "Doméstico", "Doméstico", "Kodune", "Domestique",
        "Domestique", "가정용", "家用", "家用", "Кућни", "Домашний", "Ev", "Koti",
        "Домашен", "Домашній", "Heim", "Domèstic", "Domestico", "家庭用",
        "Namų", "منزلي", "Үй", "Hjemme", "Pambahay", "घरेलू", "ในบ้าน",
        "Rumahan", "საშინაო", "Mājas", "Gia đình", "Οικιακός", "Хатні",
        "Domáci", "Hem", "خانگی",
    ],
    "Gaming PC": [
        "Gaming", "Gamer", "Gaming", "Gamer", "De gaming", "Gamingowy",
        "Herní", "Gejmerski", "Gamer", "Gamer", "Mängu", "De jeu", "De jeu",
        "게이밍", "游戏", "遊戲", "Гејмерски", "Геймерский", "Oyuncu", "Pelaaja",
        "Геймърски", "Геймерський", "Gaming", "Gamer", "Da gaming", "ゲーミング",
        "Žaidimų", "للألعاب", "Геймерлік", "Gaming", "Panlaro", "गेमिंग",
        "เกมมิ่ง", "Gaming", "სათამაშო", "Spēļu", "Chơi game", "Gaming",
        "Геймерскі", "Herný", "Gaming", "گیمینگ",
    ],
    "Cheap Miner": [
        "Prospector", "Aranyásó", "Goudzoeker", "Buscador de oro",
        "Căutător de aur", "Poszukiwacz", "Zlatokop", "Tragač za zlatom",
        "Garimpeiro", "Garimpeiro", "Kullaotsija", "Chercheur d'or",
        "Chercheur d'or", "탐광자", "淘金者", "淘金者", "Трагач за златом",
        "Старатель", "Altın arayıcı", "Kullankaivaja", "Златотърсач",
        "Старатель", "Goldsucher", "Buscador d'or", "Cercatore d'oro",
        "探鉱者", "Aukso ieškotojas", "منقب", "Алтын іздеуші", "Gullgraver",
        "Maghahanap ng ginto", "सोना खोजी", "นักแสวงหาทอง",
        "Pendulang emas", "ოქროს მაძიებელი", "Zelta meklētājs",
        "Người đãi vàng", "Χρυσοθήρας", "Стараннік", "Zlatokop",
        "Guldgrävare", "طلایاب",
    ],
    "Medium Miner": [
        "Working Rig", "Dolgozó farm", "Werkende rig", "Granja en marcha",
        "Fermă de lucru", "Farma robocza", "Pracovní farma", "Radna farma",
        "Fazenda ativa", "Quinta ativa", "Töötalu", "Ferme active",
        "Ferme active", "작업용 리그", "工作矿场", "工作礦場", "Радна фарма",
        "Рабочая ферма", "Çalışan çiftlik", "Työtila", "Работеща ферма",
        "Робоча ферма", "Arbeitsfarm", "Granja activa", "Rig da lavoro",
        "ワーキングリグ", "Darbo ferma", "مزرعة عاملة", "Жұмыс фермасы",
        "Arbeidsrigg", "Gumaganang rig", "कार्यरत रिग", "ริกทำงาน",
        "Rig kerja", "სამუშაო ფერმა", "Darba ferma", "Giàn làm việc",
        "Φάρμα εργασίας", "Працоўная ферма", "Pracovná farma", "Arbetsrigg",
        "ریگ کاری",
    ],
    "Ultra Miner": [
        "Big Guy", "Behemót", "Kanjer", "Grandullón", "Vlăjganul", "Kolos",
        "Macek", "Gromada", "Grandalhão", "Grandalhão", "Suurtükk",
        "Costaud", "Costaud", "덩치", "大块头", "大塊頭", "Громада", "Здоровяк",
        "İri yarı", "Jättiläinen", "Здравеняк", "Здоровань", "Der Brocken",
        "Grandotxo", "Omone", "デカブツ", "Stuomeningas", "الضخم", "Дәу",
        "Kjempen", "Malaking lalaki", "तगड़ा", "ตัวใหญ่", "Si Gede",
        "დიდგვამიანი", "Lielais", "Gã to con", "Θηρίο", "Здаравяк", "Macek",
        "Bjässen", "غول‌پیکر",
    ],
    "Workstation PC": [
        "Workstation", "Munkaállomás", "Werkstation", "Estación de trabajo",
        "Stație de lucru", "Stacja robocza", "Pracovní stanice",
        "Radna stanica", "Estação de trabalho", "Estação de trabalho",
        "Tööjaam", "Station de travail", "Station de travail", "워크스테이션",
        "工作站", "工作站", "Радна станица", "Рабочая станция", "İş istasyonu",
        "Työasema", "Работна станция", "Робоча станція", "Workstation",
        "Estació de treball", "Workstation", "ワークステーション", "Darbo stotis",
        "محطة عمل", "Жұмыс станциясы", "Arbeidsstasjon", "Workstation",
        "वर्कस्टेशन", "เวิร์กสเตชัน", "Workstation", "სამუშაო სადგური",
        "Darbstacija", "Máy trạm", "Σταθμός εργασίας", "Працоўная станцыя",
        "Pracovná stanica", "Arbetsstation", "ایستگاه کاری",
    ],
    # "Геймерский аквариум": корпус со стеклом, поэтому не просто аквариум.
    "Aquarium PC": [
        "Gaming Aquarium", "Gamer akvárium", "Gaming-aquarium",
        "Acuario gamer", "Acvariu gamer", "Akwarium gamingowe",
        "Herní akvárium", "Gejmerski akvarijum", "Aquário gamer",
        "Aquário gamer", "Mänguakvaarium", "Aquarium de jeu",
        "Aquarium de jeu", "게이밍 아쿠아리움", "游戏水族箱", "遊戲水族箱",
        "Гејмерски акваријум", "Геймерский аквариум", "Oyuncu akvaryumu",
        "Peliakvaario", "Геймърски аквариум", "Геймерський акваріум",
        "Gaming-Aquarium", "Aquari gamer", "Acquario gaming", "ゲーミングアクアリウム",
        "Žaidimų akvariumas", "حوض الألعاب", "Геймерлік акварium",
        "Gaming-akvarium", "Gaming na akwaryum", "गेमिंग एक्वेरियम",
        "ตู้ปลาเกมมิ่ง", "Akuarium gaming", "სათამაშო აკვარიუმი",
        "Spēļu akvārijs", "Bể cá gaming", "Ενυδρείο gaming",
        "Геймерскі акварыум", "Herné akvárium", "Gamingakvarium",
        "آکواریوم گیمینگ",
    ],
    "Dream PC": [
        "Dream Machine", "Álomgép", "Droommachine", "Máquina de ensueño",
        "Mașina de vis", "Maszyna marzeń", "Vysněný stroj", "Mašina snova",
        "Máquina dos sonhos", "Máquina de sonho", "Unistuste masin",
        "Machine de rêve", "Machine de rêve", "드림 머신", "梦想机器", "夢想機器",
        "Машина снова", "Машина мечты", "Rüya makinesi", "Unelmakone",
        "Машина мечта", "Машина мрії", "Traummaschine", "Màquina de somni",
        "Macchina dei sogni", "ドリームマシン", "Svajonių mašina", "آلة الأحلام",
        "Арман машинасы", "Drømmemaskin", "Makinang pangarap",
        "सपनों की मशीन", "เครื่องในฝัน", "Mesin impian", "ოცნების მანქანა",
        "Sapņu mašīna", "Cỗ máy mơ ước", "Μηχανή των ονείρων",
        "Машына мары", "Vysnívaný stroj", "Drömmaskin", "ماشین رویایی",
    ],
    # "Пережиток прошлого": восемь Titan V — железо мощное, но вчерашнее.
    "Titan Miner": [
        "Relic of the Past", "A múlt maradványa",
        "Relikwie uit het verleden", "Reliquia del pasado",
        "Relicvă a trecutului", "Relikt przeszłości",
        "Pozůstatek minulosti", "Relikt prošlosti", "Relíquia do passado",
        "Relíquia do passado", "Mineviku reliikvia", "Relique du passé",
        "Relique du passé", "과거의 유물", "过去的遗物", "過去的遺物", "Реликт прошлости",
        "Пережиток прошлого", "Geçmişin kalıntısı", "Menneisyyden jäänne",
        "Реликва от миналото", "Пережиток минулого",
        "Relikt der Vergangenheit", "Relíquia del passat",
        "Reliquia del passato", "過去の遺物", "Praeities relikvija",
        "من مخلفات الماضي", "Өткеннің жәдігері", "Fortidslevning",
        "Labi ng nakaraan", "अतीत का अवशेष", "ของตกทอดจากอดีต",
        "Peninggalan masa lalu", "წარსულის რელიქვია", "Pagātnes relikvija",
        "Di tích quá khứ", "Λείψανο του παρελθόντος", "Перажытак мінулага",
        "Pozostatok minulosti", "Relik från förr", "بازمانده گذشته",
    ],
    "Compact 5090 Miner": [
        "Little 5090", "Kis 5090", "Kleine 5090", "Pequeño 5090",
        "Micul 5090", "Mały 5090", "Malý 5090", "Mali 5090", "Pequeno 5090",
        "Pequeno 5090", "Väike 5090", "Petit 5090", "Petit 5090", "리틀 5090",
        "小 5090", "小 5090", "Мали 5090", "Малыш 5090", "Küçük 5090",
        "Pikku 5090", "Малкият 5090", "Малюк 5090", "Kleine 5090",
        "Petit 5090", "Piccolo 5090", "リトル5090", "Mažasis 5090",
        "الصغير 5090", "Кішкентай 5090", "Lille 5090", "Maliit na 5090",
        "छोटा 5090", "5090 ตัวเล็ก", "Si Kecil 5090", "პატარა 5090",
        "Mazais 5090", "5090 nhỏ", "Μικρό 5090", "Малы 5090", "Malý 5090",
        "Lilla 5090", "۵۰۹۰ کوچک",
    ],
    "Flagship Miner": [
        "Monster", "Szörnyeteg", "Monster", "Monstruo", "Monstru", "Potwór",
        "Monstrum", "Monstrum", "Monstro", "Monstro", "Koletis", "Monstre",
        "Monstre", "몬스터", "怪物", "怪物", "Чудовиште", "Монстр", "Canavar",
        "Hirviö", "Чудовище", "Монстр", "Monster", "Monstre", "Mostro",
        "モンスター", "Pabaisa", "وحش", "Құбыжық", "Monster", "Halimaw",
        "राक्षस", "สัตว์ประหลาด", "Monster", "მონსტრი", "Monstrs",
        "Quái vật", "Τέρας", "Пачвара", "Monštrum", "Monster", "هیولا",
    ],
    "Black": [
        "Black", "Fekete", "Zwart", "Negro", "Negru", "Czarny", "Černá",
        "Crna", "Preto", "Preto", "Must", "Noir", "Noir", "블랙", "黑色",
        "黑色", "Црна", "Чёрный", "Siyah", "Musta", "Черен", "Чорний",
        "Schwarz", "Negre", "Nero", "ブラック", "Juoda", "أسود", "Қара",
        "Svart", "Itim", "काला", "สีดำ", "Hitam", "შავი", "Melns", "Đen",
        "Μαύρο", "Чорны", "Čierna", "Svart", "مشکی",
    ],
    "White": [
        "White", "Fehér", "Wit", "Blanco", "Alb", "Biały", "Bílá", "Bela",
        "Branco", "Branco", "Valge", "Blanc", "Blanc", "화이트", "白色",
        "白色", "Бела", "Белый", "Beyaz", "Valkoinen", "Бял", "Білий",
        "Weiß", "Blanc", "Bianco", "ホワイト", "Balta", "أبيض", "Ақ",
        "Hvit", "Puti", "सफेद", "สีขาว", "Putih", "თეთრი", "Balts",
        "Trắng", "Λευκό", "Белы", "Biela", "Vit", "سفید",
    ],
    "Blue": [
        "Blue", "Kék", "Blauw", "Azul", "Albastru", "Niebieski", "Modrá",
        "Plava", "Azul", "Azul", "Sinine", "Bleu", "Bleu", "블루", "蓝色",
        "藍色", "Плава", "Синий", "Mavi", "Sininen", "Син", "Синій",
        "Blau", "Blau", "Blu", "ブルー", "Mėlyna", "أزرق", "Көк", "Blå",
        "Asul", "नीला", "สีน้ำเงิน", "Biru", "ლურჯი", "Zils", "Xanh dương",
        "Μπλε", "Сіні", "Modrá", "Blå", "آبی",
    ],
    "Red": [
        "Red", "Piros", "Rood", "Rojo", "Roșu", "Czerwony", "Červená",
        "Crvena", "Vermelho", "Vermelho", "Punane", "Rouge", "Rouge",
        "레드", "红色", "紅色", "Црвена", "Красный", "Kırmızı", "Punainen",
        "Червен", "Червоний", "Rot", "Vermell", "Rosso", "レッド",
        "Raudona", "أحمر", "Қызыл", "Rød", "Pula", "लाल", "สีแดง",
        "Merah", "წითელი", "Sarkans", "Đỏ", "Κόκκινο", "Чырвоны",
        "Červená", "Röd", "قرمز",
    ],
}

# Описания — спецификации. Названия железа интернациональны, поэтому строка
# одна и та же для всех языков (тест требует непустое значение в каждой колонке).
SPECS = {
    "Office PC Description":
        "Celeron G3920 / 8 GB / GT 440 / 500 GB HDD / 300 W / ITX",
    "Home PC Description":
        "Core i5-8400 / 16 GB / GTX 1060 / 512 GB SSD + 1 TB HDD / 500 W / ATX",
    "Gaming PC Description":
        "Core i9-12900K / 64 GB RGB / RTX 4080 / 2 TB + 1 TB SSD / 1 kW / ATX",
    "Cheap Miner Description":
        "4 x GT 1030 / Celeron G3920 / 4 GB / 128 GB SSD / 500 W",
    "Medium Miner Description":
        "8 x RX 570 / Core i3-8300 / 8 GB / 256 GB SSD / 2 x 1 kW",
    "Ultra Miner Description":
        "24 x RTX 3080 / Core i7-8700K / 32 GB / 2 x 512 GB SSD / 6 x 2 kW",
    "Workstation PC Description":
        "Ryzen 9 7950X / 512 GB RGB (8 x 64) / RTX 4080 Ti + Titan V / "
        "2 x 1 TB M.2 + 2 TB SSD / 2 kW / EXATX",
    "Aquarium PC Description":
        "Core i7-14700K / 128 GB RGB / RTX 5090 / 2 TB + 1 TB SSD / "
        "1.1 kW / Aquarium ATX",
    "Dream PC Description":
        "Ryzen 9 7950X / 512 GB RGB (8 x 64) / RTX 5090 + RTX 4080 Ti / "
        "2 x 1 TB M.2 + 2 x 2 TB SSD / 2 kW / EXATX / Aquarium",
    "Titan Miner Description":
        "8 x Titan V / Core i7-14700K / 64 GB / 1 TB SSD / 2 x 2 kW",
    # Было "24 x RTX 5090 ... 2 x 1 TB M.2 / 6 x 2 kW" — описание не совпадало
    # со сборкой: карт 16 (столько слотов у BigMiner), накопители обычные SSD
    # (слоты Drive принимают только match=0), блоков питания 4.
    "Flagship Miner Description":
        "16 x RTX 5090 / Ryzen 9 7950X / 64 GB RGB / 2 x 1 TB SSD / 4 x 2 kW",
    "Compact 5090 Miner Description":
        "8 x RTX 5090 / Ryzen 9 7950X / 64 GB RGB / 2 x 1 TB SSD / 2 x 2 kW",
}


def build_strings() -> dict[str, list[str]]:
    out: dict[str, list[str]] = {}
    for key, values in NAMES.items():
        if len(values) != len(LANGS):
            raise SystemExit(
                f"'{key}': {len(values)} переводов вместо {len(LANGS)}"
            )
        out[key] = values
    for key, spec in SPECS.items():
        out[key] = [spec] * len(LANGS)
    return out


def main() -> int:
    with open(PATH, "r", encoding="utf-8", newline="") as fh:
        text = fh.read()

    newline = "\r\n" if "\r\n" in text else "\n"
    lines = text.split(newline)
    header = lines[0].split("\t")

    if header[1:] != LANGS:
        print("Порядок языков в Translate.txt изменился — обнови LANGS",
              file=sys.stderr)
        return 1

    width = len(header)
    strings = build_strings()

    index: dict[str, int] = {}
    for i, line in enumerate(lines):
        if line:
            index.setdefault(line.split("\t", 1)[0], i)

    added = updated = 0
    for key, values in strings.items():
        row = "\t".join([key] + values)
        if len(row.split("\t")) != width:
            raise SystemExit(f"'{key}': ширина строки не совпала")
        if key in index:
            if lines[index[key]] == row:
                continue
            lines[index[key]] = row
            updated += 1
        else:
            while lines and lines[-1] == "":
                lines.pop()
            lines.append(row)
            added += 1

    body = newline.join(lines)
    if not body.endswith(newline):
        body += newline
    with open(PATH, "w", encoding="utf-8", newline="") as fh:
        fh.write(body)

    print(f"Translate.txt: добавлено {added}, обновлено {updated}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
