#!/usr/bin/env python3
"""Кастомная крышка приезжает в коробке + пароль Wi-Fi скрыт звёздочками.

Компилирует НАСТОЯЩИЕ Box.cs и CustomPaint.cs вместе с заглушками движка,
прогоняет путь доставки (заказ -> коробка -> предмет с рисунком), затем
проверяет исходники ModForge/Wi-Fi и ассеты.
"""
from pathlib import Path
import os, shutil, subprocess, sys, tempfile

ROOT = Path(__file__).resolve().parents[1]
CS = ROOT / 'tools/tests'


def unity_mono():
    """Unity везёт свой Mono (mcs.exe + mono.exe) — системный Mono не нужен."""
    bases = []
    for var in ('ProgramFiles', 'ProgramW6432'):
        base = os.environ.get(var)
        if base:
            bases.append(Path(base) / 'Unity' / 'Hub' / 'Editor')
            bases.append(Path(base) / 'Unity')
    for base in bases:
        for mono_dir in sorted(base.glob('*/Editor/Data/MonoBleedingEdge'), reverse=True):
            compiler = next((p for p in (mono_dir / 'lib/mono/4.5/mcs.exe',
                                         mono_dir / 'lib/mono/4.5/csc.exe') if p.exists()), None)
            runner = mono_dir / 'bin/mono.exe'
            if compiler is not None and runner.exists():
                return [str(runner), str(compiler)], [str(runner)]
    return None


def toolchain():
    found = unity_mono()
    if found is not None:
        return found
    return [shutil.which('mcs') or 'mcs'], [shutil.which('mono') or 'mono']


def main() -> int:
    compiler, runner = toolchain()
    with tempfile.TemporaryDirectory() as td:
        exe = Path(td) / 'custom_cover.exe'
        subprocess.run(compiler + [
            '-langversion:latest',
            '-out:' + str(exe),
            str(CS / 'custom_cover_stubs.cs'),
            str(CS / 'custom_cover_main_stubs.cs'),
            str(CS / 'custom_cover_hooks.cs'),
            str(ROOT / 'Assets/Scripts/Assembly-CSharp/Box.cs'),
            str(ROOT / 'Assets/Scripts/Assembly-CSharp/CustomPaint.cs'),
            str(CS / 'custom_cover_tests.cs'),
        ], check=True)
        return subprocess.run(runner + [str(exe)], cwd=str(ROOT)).returncode


if __name__ == '__main__':
    sys.exit(main())
