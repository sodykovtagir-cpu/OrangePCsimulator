#!/usr/bin/env python3
"""Real source + real files + separate processes. Worker locked to prove cached
rows are available BEFORE any save scan/decode, rather than a timing accident.
"""
from pathlib import Path
import subprocess,tempfile
R=Path(__file__).resolve().parents[1];C=R/'Assets/Scripts/Assembly-CSharp'
# Reuse only the existing Unity/JSON network doubles, never the test body.
source=(R/'tools/test_save_startup_and_durability.py').read_text()
stubs=source.split("harness=r'''")[1].split('class Test {')[0]
test=r'''class Test {
 static int n;static void Check(bool b,string s){n++;if(!b)throw new Exception(s);}
 static void Wait(){int i=0;while(SaveCatalog.IsScanning){Thread.Sleep(2);if(++i>10000)throw new Exception("stuck worker");}}
 static object Gate()=>typeof(SaveCatalog).GetField("gate",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).GetValue(null);
 static byte[] Hash(string p){using(var h=System.Security.Cryptography.SHA256.Create())return h.ComputeHash(File.ReadAllBytes(p));}
 static void Main(string[] a){
 UnityEngine.Application.persistentDataPath=a[0];string folder=SaveUtility.GetFolderPath(),file=Path.Combine(folder,"private.opc"),cache=Path.Combine(folder,".menu-index-v1.cache");
 if(a[1]=="write"){
  var loader=new DataLoader(file,new GameData{roomName="Cached title",hardcore=true,workshopId=17,coin=654,icon="COVER_BYTES",siteHostingWorld="PRIVATE_SITE_STATE"});loader.Content="PRIVATE_WORLD_STATE";loader.WriteToFile();
  SaveCatalog.Initialize(folder);Wait();Check(File.Exists(cache),"no disk index");Check(new FileInfo(cache).Length<4096,"cache includes full world/header");
  File.SetLastWriteTimeUtc(cache,new DateTime(2000,1,1,0,0,0,DateTimeKind.Utc));SaveCatalog.Refresh();Wait();Check(File.GetLastWriteTimeUtc(cache).Year==2000,"unchanged refresh rewrote disk cache");
  string raw=System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(cache));Check(!raw.Contains("PRIVATE_WORLD_STATE")&&!raw.Contains("PRIVATE_SITE_STATE"),"world leaked into index");
 } else if(a[1]=="read"){
  byte[] before=Hash(file);DataLoader preview;
  lock(Gate()){
   SaveCatalog.Initialize(folder);Check(SaveCatalog.IsScanning,"worker not scheduled");var rows=SaveCatalog.Snapshot();
   Check(rows.Length==1,"cached list not immediately available before worker");preview=rows[0].Loader;
   Check(preview.IsPreviewOnly,"cache marked trusted");Check(preview.GameData.roomName=="Cached title"&&preview.GameData.icon=="COVER_BYTES"&&preview.GameData.hardcore&&preview.GameData.workshopId==17,"metadata/cover not restored");
   bool rejected=false;try{preview.WriteToFile();}catch(InvalidOperationException){rejected=true;}Check(rejected,"preview can overwrite complete save");
   Check(Hash(file).SequenceEqual(before),"preview changed input");
  }
  Wait();Check(!SaveCatalog.Snapshot()[0].Loader.IsPreviewOnly,"worker failed to authenticate preview");
  var untrusted=DataLoader.FromPreview(file,new GameData{coin=int.MaxValue,roomName="Forged preview"});Check(untrusted.Content=="PRIVATE_WORLD_STATE"&&!untrusted.IsPreviewOnly&&untrusted.GameData.coin==654,"content hydration trusted cached balance");
  preview.LoadHeaderFromPath();Check(!preview.IsPreviewOnly&&preview.GameData.coin==654&&preview.GameData.siteHostingWorld=="PRIVATE_SITE_STATE","hydration lost undisplayed fields");
  preview.GameData.roomName="Edited title";preview.WriteToFile();Wait();var reopened=new DataLoader(file);reopened.LoadFromPath();Check(reopened.GameData.coin==654&&reopened.GameData.siteHostingWorld=="PRIVATE_SITE_STATE"&&reopened.Content=="PRIVATE_WORLD_STATE","edit lost world or full header");
 } else if(a[1]=="deleted"){
  SaveStorage.Delete(file);lock(Gate()){SaveCatalog.Initialize(folder);Check(SaveCatalog.Snapshot().Length==1,"missing previous cache");}Wait();Check(SaveCatalog.Snapshot().Length==0,"deleted save retained after validation");
 } else if(a[1]=="corrupt"){
  File.WriteAllBytes(cache,new byte[]{1,2,3});lock(Gate()){SaveCatalog.Initialize(folder);Check(SaveCatalog.Snapshot().Length==0,"corrupt cache accepted");}Wait();Check(SaveCatalog.Error==null,"corrupt cache broke scan");
 }
 Console.WriteLine("PASS: "+n+" persistent-cache "+a[1]+" checks");
 }}'''
with tempfile.TemporaryDirectory() as td:
 d=Path(td);(d/'test.cs').write_text(stubs+test);exe=d/'test.exe'
 sources=['SaveManagement/SaveUtility.cs','SaveManagement/DataLoader.cs','SaveManagement/SaveFileImport.cs','SaveManagement/SaveCatalog.cs','SaveManagement/SaveStorage.cs','GameData.cs']
 subprocess.run(['mcs','-r:System.Web.Extensions','-out:'+str(exe),str(d/'test.cs'),*[str(C/s)for s in sources]],check=True)
 for mode in ['write','read','deleted','corrupt']:
  subprocess.run(['mono',str(exe),str(d/'persistent'),mode],check=True,timeout=30)
print('PASS: compact persistent metadata+cover index, immediate locked-worker startup snapshot, authenticated edit, stale deletion and corrupt-cache fallback')
