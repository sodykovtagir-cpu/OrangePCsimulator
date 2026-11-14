#!/usr/bin/env python3
"""Build a standalone read-only Windows/.NET diagnostic from actual save decoder.
Requires Mono mcs; executable runs on Windows .NET Framework 4.x or Mono.
"""
from pathlib import Path
import subprocess, zipfile
R=Path(__file__).resolve().parents[1]
out=R/'artifacts/save-diagnostics';out.mkdir(parents=True,exist_ok=True)
subprocess.run(['mcs','-r:System.Web.Extensions','-platform:anycpu','-out:'+str(out/'SaveDiagnostics.exe'),str(R/'tools/SaveDiagnostics.cs'),str(R/'Assets/Scripts/Assembly-CSharp/SaveManagement/SaveUtility.cs')],check=True)
(out/'Diagnose-save.cmd').write_bytes(b'@echo off\r\nchcp 65001 >nul\r\nif "%~1"=="" (\r\n echo Drag one .opc or .pc file onto this file.\r\n pause\r\n exit /b 1\r\n)\r\n"%~dp0SaveDiagnostics.exe" "%~1" > "%~dp0save-diagnostics.txt"\r\nnotepad "%~dp0save-diagnostics.txt"\r\n')
(out/'README.txt').write_text('''Локальная диагностика сохранения (Windows, .NET Framework 4.x)

1. Распакуйте архив в отдельную доступную для записи папку.
2. Перетащите ОДИН файл .opc/.pc на Diagnose-save.cmd.
3. Откроется короткий save-diagnostics.txt. Достаточно прислать этот отчёт.
   Сам сейв никуда отправлять не нужно. Можно проверять копию файла.

Утилита только читает исходный файл. Ничего не исправляет, не пересохраняет,
не снимает проверку подписи и не обращается к сети. Отчёт не содержит имени
мира, пути файла, содержимого компьютеров, картинок, ключей или аккаунтов.
Повторный запуск заменяет только save-diagnostics.txt в папке утилиты.

Это самостоятельно собранная неподписанная .NET-утилита: Windows может показать
предупреждение. Не отключайте защиту системы. Исходники и сборочный скрипт
находятся в tools/SaveDiagnostics.cs и tools/build_save_diagnostics.py.
Если запуск заблокирован, можно использовать исходники или обычный Player.log.

JSON проверяется не Unity/Newtonsoft, а .NET-парсером: несовпадение парсеров
не является доказательством порчи. Утилита не воссоздаёт объекты Unity/Lua.
На Linux/macOS при установленном Mono:
mono SaveDiagnostics.exe /путь/к/файлу.opc > save-diagnostics.txt
''',encoding='utf-8')
zip_path=R/'artifacts/Save-diagnostics.zip'
with zipfile.ZipFile(zip_path,'w',zipfile.ZIP_DEFLATED) as z:
 for p in sorted(out.iterdir()):
  if p.name!='save-diagnostics.txt':z.write(p,p.name)
print(zip_path)
