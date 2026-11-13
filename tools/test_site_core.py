#!/usr/bin/env python3
"""Execute real C# package/history/Lua code. JSON/Unity.Random are engine doubles;
not a Unity JsonUtility compatibility, UI or rendered-pixel test.
"""
from pathlib import Path
import subprocess,tempfile,shutil
R=Path(__file__).resolve().parents[1]
C=R/'Assets/Scripts/Assembly-CSharp/PC/Component/Software'
assert 'PcosLuaHost.Bind' not in (C/'Sites/SiteLua.cs').read_text()
if not(shutil.which('mcs') and shutil.which('mono')):raise SystemExit('Mono/mcs required')
with tempfile.TemporaryDirectory() as td:
    exe=Path(td)/'test.exe'
    subprocess.run(['mcs','-r:System.Web.Extensions','-out:'+str(exe),str(R/'tools/tests/site_core_stubs.cs'),str(R/'tools/tests/site_core_tests.cs'),str(C/'Lua/PcosLua.cs'),*[str(C/'Sites'/n) for n in ['SiteDocument.cs','SiteHistory.cs','SiteLua.cs']]],check=True)
    subprocess.run(['mono',str(exe)],check=True,timeout=20)
