#!/usr/bin/env python3
"""Write the 3DMork translation rows from tools/3dmork_i18n.py into Translate.txt.

The applier is idempotent: a key that is already present is rewritten in place,
a new key is appended at the end of the table. The file keeps its original
encoding, byte order mark and line endings, and no other row is touched.
"""
import io
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import importlib.util

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(HERE)
TRANSLATE = os.path.join(REPO, "Assets", "Resources", "Translate.txt")
I18N = os.path.join(HERE, "3dmork_i18n.py")

spec = importlib.util.spec_from_file_location("dm3_i18n", I18N)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
LANGS = module.LANGS
STRINGS = module.STRINGS


def placeholder_pattern(value):
    return sorted(re.findall(r"\{\d+\}", value))


def main():
    with io.open(TRANSLATE, "r", encoding="utf-8-sig", newline="") as handle:
        raw = handle.read()

    bom = ""
    if raw and raw[0] == "﻿":
        bom = raw[0]
        raw = raw[1:]
    crlf = "\r\n" in raw
    if crlf:
        raw = raw.replace("\r\n", "\n")

    lines = raw.split("\n")
    # a stray empty line breaks every TSV consumer, drop those rows
    blank = [number for number, line in enumerate(lines) if number and not line.strip()]
    for number in reversed(blank):
        del lines[number]
    while lines and not lines[-1].strip():
        lines.pop()
    trailing = raw.endswith("\n")

    header = lines[0].split("\t")
    if header[0] != "*Short Form":
        raise SystemExit("unexpected header row: %r" % header[0])
    if header[1:] != LANGS:
        raise SystemExit("language columns differ:\n  file: %s\n  i18n: %s" % (header[1:], LANGS))

    # reference placeholders per language must match, mirroring the C# test
    for key, values in STRINGS.items():
        if placeholder_pattern(values[0]) != placeholder_pattern(values[17]):
            raise SystemExit("%s: RU placeholders differ from EN" % key)

    index_of = {}
    for number, line in enumerate(lines):
        if number == 0:
            continue
        index_of.setdefault(line.split("\t")[0], number)

    added = updated = 0
    for key, values in STRINGS.items():
        row = "\t".join([key] + list(values))
        if key in index_of:
            if lines[index_of[key]] == row:
                continue
            lines[index_of[key]] = row
            updated += 1
        else:
            lines.append(row)
            added += 1

    out = "\n".join(lines) + ("\n" if trailing or True else "")
    if crlf:
        out = out.replace("\n", "\r\n")
    with io.open(TRANSLATE, "w", encoding="utf-8", newline="") as handle:
        handle.write(bom + out)

    print("3DMork rows: %d added, %d updated, %d total keys, %d columns, %d blank rows removed"
          % (added, updated, len(STRINGS), len(LANGS), len(blank)))


if __name__ == "__main__":
    main()
