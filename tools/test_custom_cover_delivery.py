#!/usr/bin/env python3
"""Кастомная крышка приезжает в коробке + пароль Wi-Fi скрыт звёздочками.

Компилирует НАСТОЯЩИЕ Box.cs и CustomPaint.cs вместе с заглушками движка,
прогоняет путь доставки (заказ -> коробка -> предмет с рисунком), затем
проверяет исходники ModForge/Wi-Fi и ассеты.
"""
from pathlib import Path
import subprocess, sys, tempfile

ROOT = Path(__file__).resolve().parents[1]
CS = ROOT / 'tools/tests'


def main() -> int:
    with tempfile.TemporaryDirectory() as td:
        exe = Path(td) / 'custom_cover.exe'
        subprocess.run([
            'mcs', '-out:' + str(exe),
            str(CS / 'custom_cover_stubs.cs'),
            str(CS / 'custom_cover_main_stubs.cs'),
            str(CS / 'custom_cover_hooks.cs'),
            str(ROOT / 'Assets/Scripts/Assembly-CSharp/Box.cs'),
            str(ROOT / 'Assets/Scripts/Assembly-CSharp/CustomPaint.cs'),
            str(CS / 'custom_cover_tests.cs'),
        ], check=True)
        return subprocess.run(['mono', str(exe)], cwd=str(ROOT)).returncode


if __name__ == '__main__':
    sys.exit(main())
