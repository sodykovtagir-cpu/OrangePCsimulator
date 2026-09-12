#!/usr/bin/env python3
"""Починка .meta текстур, сохранённых новой версией Unity.

Проект собирается на Unity 2022.3.51f1, но часть текстур получила .meta от
Unity 6 (TextureImporter serializedVersion: 13). Импортёр 2022.3 такой файл
не понимает и падает с «Unknown error occurred while loading
'Assets/Texture2D/....png'» — картинка не грузится, а в магазине вместо
иконки пустое место.

Скрипт приводит такие .meta к serializedVersion 12:

* убирает поля, которых в 2022.3 нет:
  flipGreenChannel, swizzle, ignorePlatformSupport, mipmapLimitGroupName;
* переименовывает ignoreMipmapLimit -> ignoreMasterTextureLimit;
* правит значения, изменившие смысл: textureFormat 1 -> 34 (Automatic),
  textureCompression 1 -> 2 (NormalQuality), alignment 0 -> 9 (Custom при
  заданном spritePivot), flipbookRows/Columns 1 -> 0.

guid не трогается, поэтому все ссылки на текстуры остаются рабочими.

Запуск:
    python3 tools/fix_texture_meta.py            # починить
    python3 tools/fix_texture_meta.py --check    # только показать
"""

from __future__ import annotations

import argparse
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# Поля, появившиеся после 2022.3 — импортёр на них и спотыкается.
DROP_FIELDS = (
    "flipGreenChannel",
    "swizzle",
    "ignorePlatformSupport",
    "mipmapLimitGroupName",
)

RENAME_FIELDS = {
    "ignoreMipmapLimit": "ignoreMasterTextureLimit",
}


def convert(text: str) -> str:
    """Привести содержимое .meta текстуры к формату Unity 2022.3."""
    out = []
    for line in text.split("\n"):
        stripped = line.strip()
        key = stripped.split(":", 1)[0] if ":" in stripped else ""

        if key in DROP_FIELDS:
            continue

        if key in RENAME_FIELDS:
            indent = line[: len(line) - len(line.lstrip())]
            value = stripped.split(":", 1)[1]
            out.append(f"{indent}{RENAME_FIELDS[key]}:{value}")
            continue

        out.append(line)

    text = "\n".join(out)

    # serializedVersion самого TextureImporter (вложенные блоки не трогаем).
    text = re.sub(r"^  serializedVersion: 13$", "  serializedVersion: 12",
                  text, count=1, flags=re.M)

    # TextureImporterFormat: 1 (Alpha8) в новой схеме означает Automatic,
    # в 2022.3 Automatic — это 34. Иначе иконка импортируется как Alpha8
    # и выглядит чёрным квадратом.
    text = re.sub(r"^  textureFormat: 1$", "  textureFormat: 34",
                  text, count=1, flags=re.M)

    # Сжатие: 1 -> 2 (NormalQuality), как во всех остальных .meta проекта.
    text = re.sub(r"^(\s+)textureCompression: 1$", r"\1textureCompression: 2",
                  text, flags=re.M)

    # Спрайт с явным spritePivot должен иметь alignment 9 (Custom),
    # иначе пивот игнорируется и иконка съезжает.
    if "spritePivot: {x: 0.5, y: 0.5}" in text:
        text = re.sub(r"^  alignment: 0$", "  alignment: 9",
                      text, count=1, flags=re.M)

    text = re.sub(r"^  flipbookRows: 1$", "  flipbookRows: 0",
                  text, count=1, flags=re.M)
    text = re.sub(r"^  flipbookColumns: 1$", "  flipbookColumns: 0",
                  text, count=1, flags=re.M)

    return text


def needs_fix(text: str) -> bool:
    return re.search(r"^  serializedVersion: 13$", text, flags=re.M) is not None


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true",
                    help="только показать список, ничего не менять")
    args = ap.parse_args()

    broken = []
    for dirpath, _dirs, files in os.walk(os.path.join(REPO, "Assets")):
        for fn in files:
            if not fn.endswith(".meta"):
                continue
            path = os.path.join(dirpath, fn)
            with open(path, encoding="utf-8", errors="replace") as fh:
                text = fh.read()
            if "TextureImporter:" not in text or not needs_fix(text):
                continue
            broken.append((path, text))

    if not broken:
        print("Все .meta текстур совместимы с Unity 2022.3.")
        return 0

    for path, text in broken:
        rel = os.path.relpath(path, REPO)
        if args.check:
            print(f"  требует починки: {rel}")
            continue
        fixed = convert(text)
        with open(path, "w", encoding="utf-8") as fh:
            fh.write(fixed)
        print(f"  починено: {rel}")

    print(f"\nВсего: {len(broken)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
