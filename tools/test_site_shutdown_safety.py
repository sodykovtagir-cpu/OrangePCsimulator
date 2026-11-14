#!/usr/bin/env python3
"""Execute real selected shutdown/reset methods with engine/hardware doubles."""
from pathlib import Path
import subprocess,tempfile,sys
sys.path.insert(0,str(Path(__file__).parent))
from server_assets import Prefab,ROOT
C=ROOT/'Assets/Scripts/Assembly-CSharp/PC/Component'
def method(file,start):
 s=(C/file).read_text();a=s.index(start);b=s.index('{',a);depth=1;i=b+1
 while depth:
  if s[i]=='{':depth+=1
  if s[i]=='}':depth-=1
  i+=1
 return s[a:i]
board=method('Motherboard.cs','public void PowerOff(')
case=method('Case.cs','private void DisconnectBoard(')
monitor=(C/'Monitor.cs').read_text()
code='''using System;using UnityEngine;
namespace UnityEngine {
 public class Object {public bool destroyed;public static bool operator ==(Object a,Object b){return (ReferenceEquals(a,null)||a.destroyed)?(ReferenceEquals(b,null)||b.destroyed):ReferenceEquals(a,b);}public static bool operator !=(Object a,Object b){return !(a==b);}public static implicit operator bool(Object a){return a!=null;}public override bool Equals(object o){return ReferenceEquals(this,o);}public override int GetHashCode(){return base.GetHashCode();}}
 public class Transform:Object {public int sets;public void SetParent(Transform p){sets++;}}
 public class Canvas:Object {Transform t=new Transform();public Transform transform {get{if(destroyed)throw new Exception("MissingReference");return t;}}public RenderMode renderMode;}
 public enum RenderMode {WorldSpace}
 public class SerializeField:Attribute{}
}
public static class SaveManager {public static bool RestoringSave;}
public class HardwareSlot {public void Switch(bool v){}}
namespace PC.Component.Software.Sites {public class SiteHostingService {public static SiteHostingService Instance=new SiteHostingService();public int stops;public void HostStopped(int host){stops++;}}}
namespace PC.Component.Software.OS {public static class OverclockBoot {public static void Apply(object b,object p){}}}
namespace PC.Component.Cabling {public static class CablePower {public static void RefreshAll(){}}}
namespace PC.Component {
 public class Display:UnityEngine.Object {protected Canvas canvas;protected virtual void ResetCanvas(){}public void AllowZoom(bool v){}public void Switch(bool a,bool b){} }
 public class SystemStub {public object gameObject;}
 public class Motherboard:UnityEngine.Object {public int Id=123,powerEpoch;public bool running=true,overclockRebootScheduled;public bool Running=>running;public Display monitor;public SystemStub System;public event Action<bool> PowerChanged;public void RemoveExternal(object e){}public void Switch(bool a,bool b){}public void SwitchBuiltIn(bool a){}public object[] GetAllSlots(){return null;}public void Destroy(object o){}public void Invoke(string s,float t){}
 BOARD
 }
 public class Case {public Motherboard ActiveBoard;public object external;public event Action<Motherboard> BoardChanged;void UpdatePower(bool p){} CASE public void Disconnect(bool report){DisconnectBoard(report);} }
 public class TestMonitor:Monitor {public void Setup(Canvas c){canvas=c;}public void Run(){ResetCanvas();}}
}
class Test {static int n;static void Check(bool v){n++;if(!v)throw new Exception("shutdown "+n);}static void Main(){
 var service=PC.Component.Software.Sites.SiteHostingService.Instance;var board=new PC.Component.Motherboard();board.PowerOff();Check(!board.Running&&service.stops==1);
 board=new PC.Component.Motherboard();board.PowerOff(false,false);Check(service.stops==1&&!board.Running);
 SaveManager.RestoringSave=true;board=new PC.Component.Motherboard();board.PowerOff();Check(service.stops==1);SaveManager.RestoringSave=false;
 var c=new PC.Component.Case{ActiveBoard=new PC.Component.Motherboard()};c.Disconnect(false);Check(service.stops==1&&c.ActiveBoard==null);
 c.ActiveBoard=new PC.Component.Motherboard();c.Disconnect(true);Check(service.stops==2&&c.ActiveBoard==null);
 var monitor=new PC.Component.TestMonitor();monitor.Run();monitor.Setup(new Canvas{destroyed=true});monitor.Run();Check(true);
 var canvas=new Canvas();monitor.Setup(canvas);typeof(PC.Component.Monitor).GetField("point",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(monitor,new Transform());monitor.Run();Check(canvas.transform.sets==1&&canvas.renderMode==RenderMode.WorldSpace);
 Console.WriteLine("PASS: "+n+" actual shutdown/reset method checks: physical stop, teardown/restore suppression, destroyed Canvas guard and normal reset");}}
'''.replace('BOARD',board).replace('CASE',case)
with tempfile.TemporaryDirectory()as td:
 file=Path(td)/'test.cs';file.write_text(code);exe=Path(td)/'test.exe'
 subprocess.run(['mcs','-out:'+str(exe),str(file),str(C/'Monitor.cs')],check=True);subprocess.run(['mono',str(exe)],check=True)
p=Prefab(ROOT/'Assets/Resources/components/CRT_Monitor.prefab');trs={d['m_GameObject']['fileID']:(i,d)for i,d in p.objects(4)}
for _,d in p.objects(65):
 _,tr=trs[d['m_GameObject']['fileID']]
 for v in d['m_Size'].values():assert v>=0
 while True:
  assert all(v>0 for v in tr['m_LocalScale'].values()),tr
  parent=tr['m_Father']['fileID']
  if not parent:break
  tr=p.data(parent)
assert 'DisconnectBoard(false);'in (C/'Case.cs').read_text()
pause=(ROOT/'Assets/Scripts/Assembly-CSharp/PauseMenu.cs').read_text();assert pause.count('LeavingWorld = true;')==2
os=(C/'Software/OS/OperatingSystem.cs').read_text();assert 'HostStopped(Board.Id)'in os and 'ramMb += ServerMemoryMb;'in os
assert 'Server.exe'in (C/'Software/TaskManager.cs').read_text()
print('PASS: CRT collider ancestor scales, real shutdown/BSOD hooks, exit markers and Task Manager/RAM integration wiring')
