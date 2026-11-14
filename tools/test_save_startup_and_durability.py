#!/usr/bin/env python3
"""Actual save IO/cache/import code, real files, separate process restart.
JSON is a .NET double; Android lifecycle/device storage still needs Play Mode/APK.
"""
from pathlib import Path
import subprocess,tempfile
R=Path(__file__).resolve().parents[1];C=R/'Assets/Scripts/Assembly-CSharp'
harness=r'''using System;using System.IO;using System.Linq;using System.Threading;using System.Web.Script.Serialization;using SaveManagement;
namespace UnityEngine {
 public enum CompressionLevel{None,Optimal}
 public static class Application {public static string persistentDataPath,streamingAssetsPath;}
 public static class JsonUtility {public static string ToJson(object v){return new JavaScriptSerializer().Serialize(v);}public static T FromJson<T>(string v){return new JavaScriptSerializer().Deserialize<T>(v);}}
}
namespace UnityEngine.Networking {
 public class DownloadHandler {public string text;}
 public class UnityWebRequestAsyncOperation {public event Action<UnityWebRequestAsyncOperation> completed;}
 public class UnityWebRequest {public enum Result {ConnectionError,ProtocolError,Success}public Result result;public string error;public DownloadHandler downloadHandler=new DownloadHandler();public static UnityWebRequest Get(string s){return new UnityWebRequest();}public UnityWebRequestAsyncOperation SendWebRequest(){return new UnityWebRequestAsyncOperation();}}
}
namespace PC.Component.Software.Sites{[Serializable] public class SiteHostingLedger{public string owner;public long charged;}}
class Test {
 static int n;static void Check(bool v,string why){n++;if(!v)throw new Exception(why);}
 static void Wait(){int tries=0;while(SaveCatalog.IsScanning){Thread.Sleep(5);if(++tries>2000)throw new Exception("worker stuck");}}
 static DataLoader Load(string p){var l=new DataLoader(p);l.LoadFromPath();return l;}
 static void Main(string[] a){UnityEngine.Application.persistentDataPath=a[0];string folder=SaveUtility.GetFolderPath();
 if(a[1]=="read"){
  SaveCatalog.Initialize(folder);Wait();var rows=SaveCatalog.Snapshot();Check(rows.Length==4,"restart enumeration");Check(rows.Count(x=>x.Loader!=null)==3,"restart recovered usable files");
  var persistent=Path.Combine(folder,"Телефон.opc");Check(Load(persistent).Content=="played world","restart kept imported progress after cache deletion");
  Check(Load(Path.Combine(folder,"interrupted.opc")).Content=="recovered world","restart recovered interrupted import");Console.WriteLine("PASS: "+n+" separate-process restart checks");return;
 }
 string cache=Path.Combine(a[0],"android-cache");Directory.CreateDirectory(cache);string src=Path.Combine(cache,"selected.OPC");
 var origin=new DataLoader(src,new GameData{roomName="Телефон",coin=100});origin.Content="imported world";origin.WriteToFile();
 string imported=SaveFileImport.Import(src,"Телефон.OPC");Check(imported.StartsWith(folder)&&File.Exists(imported),"private import committed");
 Directory.Delete(cache,true);Check(Load(imported).Content=="imported world","cache cleanup destroyed import");
 var game=Load(imported);game.GameData.coin=321;game.Content="played world";game.WriteToFile();Check(Load(imported).GameData.coin==321,"imported progress not persisted");Check(File.Exists(imported+".bak"),"missing previous good copy");
 // Full cache runs on a worker and unchanged headers are reused, not re-read.
 SaveCatalog.Initialize(folder);Wait();var first=SaveCatalog.Snapshot();Check(first.Length==1&&first[0].Loader.GameData.roomName=="Телефон","initial index");
 var header=first[0].Loader;SaveCatalog.Refresh();Wait();Check(object.ReferenceEquals(header,SaveCatalog.Snapshot()[0].Loader),"unchanged header not cached");
 Check(header.Content=="played world","lazy world hydration");
 game.GameData.roomName="Changed";game.WriteToFile();Wait();Check(!object.ReferenceEquals(header,SaveCatalog.Snapshot()[0].Loader)&&SaveCatalog.Snapshot()[0].Loader.GameData.roomName=="Changed","cache failed to invalidate");
 // Pending recovery beats backup, without replacing an existing good main file.
 string p=Path.Combine(folder,"interrupted.opc");var pending=new DataLoader(p+".pending",new GameData{roomName="Interrupted"});pending.Content="recovered world";pending.WriteToFile();
 File.WriteAllText(p,"broken");SaveStorage.RecoverDirectory(folder);Check(Load(p).Content=="recovered world","pending recovery");
 File.WriteAllText(imported+".pending","partial bytes");SaveStorage.RecoverDirectory(folder);Check(Load(imported).Content=="played world","good primary replaced by partial transaction");
 File.WriteAllText(p+".pending","partial bytes");File.Copy(p,p+".bak",true);File.WriteAllText(p,"truncated");SaveStorage.RecoverDirectory(folder);Check(Load(p).Content=="recovered world","backup recovery");
 string duplicate=SaveFileImport.Import(imported,"Телефон.opc");Check(duplicate!=imported&&File.Exists(duplicate),"duplicate import overwrote save");SaveStorage.Delete(duplicate);SaveStorage.RecoverDirectory(folder);Check(!File.Exists(duplicate)&&!File.Exists(duplicate+".bak"),"deleted save resurrected");
 bool rejected=false;try{SaveFileImport.Import(imported,"virus.exe");}catch(NotSupportedException){rejected=true;}Check(rejected,"invalid extension accepted");
 string bad=Path.Combine(folder,"corrupt.opc");File.WriteAllText(bad,"broken");SaveCatalog.Refresh();Wait();Check(SaveCatalog.Snapshot().Any(e=>e.Path==bad&&e.Loader==null&&!string.IsNullOrEmpty(e.Error)),"corruption silently discarded");
 // Stage a new interrupted file for next launch; do not let an in-memory cache mask it.
 var bootPending=new DataLoader(Path.Combine(folder,"boot-recover.opc.pending"),new GameData{roomName="Boot recovery"});bootPending.Content="boot recovered";bootPending.WriteToFile();
 SaveCatalog.Refresh();Wait();Console.WriteLine("PASS: "+n+" actual import/durable write/cache/recovery checks");
 }}'''
with tempfile.TemporaryDirectory()as td:
 d=Path(td);f=d/'test.cs';f.write_text(harness);exe=d/'test.exe'
 sources=['SaveManagement/SaveUtility.cs','SaveManagement/DataLoader.cs','SaveManagement/SaveFileImport.cs','SaveManagement/SaveCatalog.cs','SaveManagement/SaveStorage.cs','GameData.cs']
 subprocess.run(['mcs','-r:System.Web.Extensions','-out:'+str(exe),str(f),*[str(C/s)for s in sources]],check=True)
 for mode in ['write','read']:subprocess.run(['mono',str(exe),str(d/'persistent'),mode],check=True,timeout=30)
menu=(C/'FileMenu.cs').read_text();startup=(C/'SaveStartup.cs').read_text();main=(C/'MainMenu.cs').read_text()
assert 'BeforeSplashScreen' in startup and 'BeforeSceneLoad' in startup
assert 'Directory.GetFiles'not in menu and 'File.ReadAllText'not in menu and 'LoadHeaderFromPath'not in menu
assert 'Task.Run' in (C/'SaveManagement/SaveCatalog.cs').read_text() and 'BuildList(++listGeneration)'in menu
assert 'SaveCoverCache.Get' in menu and 'SaveCoverCache.Get' in startup and 'Task.Run' in main
assert 'SaveStorage.Delete(load.loader.Path)'in menu
print('PASS: earliest splash hook, worker-only save indexing/open, cached covers, bounded frame-batched list and transaction-aware deletion wiring')
