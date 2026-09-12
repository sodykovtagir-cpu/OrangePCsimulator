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
    "Office PC": [
        "Office PC", "Irodai PC", "Kantoor-pc", "PC de oficina", "PC de birou",
        "PC biurowy", "Kancelářské PC", "Kancelarijski PC", "PC de escritório",
        "PC de escritório", "Kontoriarvuti", "PC de bureau", "PC de bureau",
        "사무용 PC", "办公电脑", "辦公電腦", "Канцеларијски рачунар",
        "Офисный ПК", "Ofis PC", "Toimistotietokone", "Офис компютър",
        "Офісний ПК", "Büro-PC", "PC d'oficina", "PC da ufficio", "オフィスPC",
        "Biuro kompiuteris", "حاسوب مكتبي", "Кеңсе ДК", "Kontor-PC",
        "PC pang-opisina", "ऑफिस पीसी", "พีซีสำนักงาน", "PC kantor",
        "საოფისე კომპიუტერი", "Biroja dators", "PC văn phòng",
        "PC γραφείου", "Офісны ПК", "Kancelárske PC", "Kontorsdator",
        "رایانه اداری",
    ],
    "Home PC": [
        "Home PC", "Otthoni PC", "Thuis-pc", "PC doméstico", "PC de acasă",
        "PC domowy", "Domácí PC", "Kućni PC", "PC doméstico", "PC doméstico",
        "Koduarvuti", "PC familial", "PC familial", "가정용 PC", "家用电脑",
        "家用電腦", "Кућни рачунар", "Домашний ПК", "Ev PC'si",
        "Kotitietokone", "Домашен компютър", "Домашній ПК", "Heim-PC",
        "PC domèstic", "PC domestico", "ホームPC", "Namų kompiuteris",
        "حاسوب منزلي", "Үй ДК", "Hjemme-PC", "PC pambahay", "होम पीसी",
        "พีซีสำหรับบ้าน", "PC rumahan", "საშინაო კომპიუტერი",
        "Mājas dators", "PC gia đình", "Οικιακό PC", "Хатні ПК",
        "Domáce PC", "Hemdator", "رایانه خانگی",
    ],
    "Gaming PC": [
        "Gaming PC", "Gamer PC", "Game-pc", "PC gaming", "PC de gaming",
        "PC dla graczy", "Herní PC", "Gejmerski PC", "PC gamer", "PC gamer",
        "Mänguarvuti", "PC de jeu", "PC de jeu", "게이밍 PC", "游戏电脑",
        "遊戲電腦", "Гејмерски рачунар", "Игровой ПК", "Oyun PC'si",
        "Pelitietokone", "Геймърски компютър", "Ігровий ПК", "Gaming-PC",
        "PC gaming", "PC da gaming", "ゲーミングPC", "Žaidimų kompiuteris",
        "حاسوب ألعاب", "Ойын ДК", "Gaming-PC", "PC panlaro", "गेमिंग पीसी",
        "พีซีเกมมิ่ง", "PC gaming", "სათამაშო კომპიუტერი",
        "Spēļu dators", "PC chơi game", "PC gaming", "Гульнявы ПК",
        "Herné PC", "Speldator", "رایانه گیمینگ",
    ],
    "Cheap Miner": [
        "Cheap Miner", "Olcsó bányász", "Goedkope miner", "Minero barato",
        "Miner ieftin", "Tania koparka", "Levný těžební stroj",
        "Jeftini rudar", "Mineradora barata", "Mineradora barata",
        "Odav kaevur", "Mineur économique", "Mineur économique",
        "저가형 채굴기", "廉价矿机", "廉價礦機", "Јефтини рудар",
        "Дешёвый майнер", "Ucuz madenci", "Halpa louhija", "Евтин копач",
        "Дешевий майнер", "Günstiger Miner", "Miner barat", "Miner economico",
        "低価格マイナー", "Pigus kasėjas", "معدّن رخيص", "Арзан майнер",
        "Billig miner", "Murang miner", "सस्ता माइनर", "เครื่องขุดราคาถูก",
        "Penambang murah", "იაფი მაინერი", "Lēts mainaris", "Máy đào giá rẻ",
        "Φθηνός miner", "Танны майнер", "Lacný ťažobný stroj", "Billig miner",
        "ماینر ارزان",
    ],
    "Medium Miner": [
        "Medium Miner", "Közepes bányász", "Gemiddelde miner", "Minero medio",
        "Miner mediu", "Średnia koparka", "Střední těžební stroj",
        "Srednji rudar", "Mineradora média", "Mineradora média",
        "Keskmine kaevur", "Mineur intermédiaire", "Mineur intermédiaire",
        "중급 채굴기", "中端矿机", "中階礦機", "Средњи рудар",
        "Средний майнер", "Orta seviye madenci", "Keskitason louhija",
        "Среден копач", "Середній майнер", "Mittlerer Miner", "Miner mitjà",
        "Miner medio", "中級マイナー", "Vidutinis kasėjas", "معدّن متوسط",
        "Орташа майнер", "Middels miner", "Katamtamang miner",
        "मध्यम माइनर", "เครื่องขุดระดับกลาง", "Penambang menengah",
        "საშუალო მაინერი", "Vidējs mainaris", "Máy đào tầm trung",
        "Μεσαίος miner", "Сярэдні майнер", "Stredný ťažobný stroj",
        "Medelstor miner", "ماینر متوسط",
    ],
    "Ultra Miner": [
        "Ultra Miner", "Ultra bányász", "Ultra-miner", "Minero ultra",
        "Miner ultra", "Koparka ultra", "Ultra těžební stroj", "Ultra rudar",
        "Mineradora ultra", "Mineradora ultra", "Ultra kaevur", "Mineur ultra",
        "Mineur ultra", "울트라 채굴기", "旗舰矿机", "旗艦礦機",
        "Ултра рудар", "Ультра майнер", "Ultra madenci", "Ultra-louhija",
        "Ултра копач", "Ультра майнер", "Ultra-Miner", "Miner ultra",
        "Miner ultra", "ウルトラマイナー", "Ultra kasėjas", "معدّن فائق",
        "Ультра майнер", "Ultra-miner", "Ultra miner", "अल्ट्रा माइनर",
        "เครื่องขุดอัลตร้า", "Penambang ultra", "ულტრა მაინერი",
        "Ultra mainaris", "Máy đào ultra", "Ultra miner", "Ультра майнер",
        "Ultra ťažobný stroj", "Ultra-miner", "ماینر اولترا",
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
