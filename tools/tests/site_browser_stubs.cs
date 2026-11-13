// Browser external services are doubles; the complete Browser and BrowserSites
// sources are compiled, not reimplemented here. No rendered-Unity claim.
using System;
namespace UnityEngine {
 public static class Debug{public static void LogWarning(object o){}}
 [AttributeUsage(AttributeTargets.Field)]public class Header:Attribute{public Header(string s){}}
 [AttributeUsage(AttributeTargets.Field)]public class Min:Attribute{public Min(float n){}}
 [AttributeUsage(AttributeTargets.Field)]public class TextArea:Attribute{public TextArea(int a,int b){}}
 public class WaitForSecondsRealtime{public WaitForSecondsRealtime(float t){}}
}
namespace PC.Component.Networking {
 public class RouterDevice{}
 public class ConnectionStatus{public float Quality=1;}
 public class InternetService {
  public static InternetService Instance;public int transfers;
  public bool CanOpenLocalPanel(object board,out RouterDevice router){router=null;return true;}
  public bool TryTransfer(object board,string url,float mb,out float delay,out string error){transfers++;delay=0;error=null;return true;}
  public void SignalTraffic(object board){}public ConnectionStatus Status(object board){return new ConnectionStatus();}
 }
}
namespace PC.Component.Software {
 public class Website:UnityEngine.Component {public UnityEngine.Sprite icon;public void Init(OS.OperatingSystem s){}}
 public class FavoriteTile:UnityEngine.Component {public void Initialize(string url,UnityEngine.Sprite icon,Action<string> open,Action<string> remove){}}
 public class OggleResultItem:UnityEngine.Component{public UnityEngine.UI.Text title,description;}
 public static class RouterWebPages {
  public static bool IsLocalAddress(string url){return url=="192.168.1.1";}
  public static UnityEngine.GameObject Create(Browser b,UnityEngine.Transform p,object board,string url){return null;}
  public static UnityEngine.GameObject Message(UnityEngine.Transform p,string text){return null;}
 }
}
