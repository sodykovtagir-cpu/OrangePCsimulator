#!/usr/bin/env python3
"""Read-only CLI reports: actual decoder, real files; no world data in report."""
from pathlib import Path
import subprocess,tempfile,hashlib
R=Path(__file__).resolve().parents[1]; decoder=R/'Assets/Scripts/Assembly-CSharp/SaveManagement/SaveUtility.cs'
front=R/'tools/SaveDiagnostics.cs'
generator=r'''using System;using System.IO;using System.Reflection;using SaveManagement;
class Fixture {static void Main(string[] a) {
 string h="{\"version\":\"1.8.3\",\"roomName\":\"PRIVATE_WORLD_NAME\",\"siteHostingWorld\":\""+new string('a',9*1024*1024)+"\",\"otherCableMode\":{\"content\":\"{\\\"itemData\\\":[]}\"}}";
 string payload=h+"\n{\"playerData\":{},\"itemData\":[],\"private\":\"PRIVATE_CONTENT\"}";
 File.WriteAllText(Path.Combine(a[0],"modern.opc"),SaveUtility.Encode(payload));
 var m=typeof(SaveUtility).GetMethod("ComputeHmac",BindingFlags.Static|BindingFlags.NonPublic);
 string sig=(string)m.Invoke(null,new object[]{payload});
 File.WriteAllText(Path.Combine(a[0],"old.opc"),SaveUtility.EncryptDecrypt(SaveUtility.Tag+sig+"\n"+payload));
 string broken=SaveUtility.Encode(payload);broken=broken.Substring(0,broken.Length-20);
 File.WriteAllText(Path.Combine(a[0],"broken.opc"),broken);
}}'''
with tempfile.TemporaryDirectory() as td:
 d=Path(td);(d/'gen.cs').write_text(generator)
 for target,entry,extra in [('diag','SaveDiagnostics',[]),('gen','Fixture',[str(d/'gen.cs')])]:
  subprocess.run(['mcs','-r:System.Web.Extensions','-main:'+entry,'-out:'+str(d/(target+'.exe')),str(front),str(decoder),*extra],check=True)
 subprocess.run(['mono',str(d/'gen.exe'),td],check=True)
 for name in ['modern','old','broken']:
  p=d/(name+'.opc');before=hashlib.sha256(p.read_bytes()).digest()
  report=subprocess.check_output(['mono',str(d/'diag.exe'),str(p)],text=True)
  assert hashlib.sha256(p.read_bytes()).digest()==before
  for private in ['PRIVATE_WORLD_NAME','PRIVATE_CONTENT',td,'OrangePCSim-SaveInt']:
   assert private not in report
  if name=='broken':assert 'HMAC mismatch' in report and 'Full decode status: Tampered' in report
  else:
   assert 'Full decode status: Ok' in report and 'GameData: JSON object OK' in report
   assert 'World: JSON object OK' in report and 'Alternate world: JSON object OK' in report
   assert 'itemData count: 0' in report and 'Game version: 1.8.3' in report
print('PASS: standalone diagnostic reads modern/old/invalid files; input hashes unchanged; reports omit private contents')
