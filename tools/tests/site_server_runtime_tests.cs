using System;using System.Collections.Generic;using System.Reflection;using PC.Component.Software.Sites;using UnityEngine;
class ServerRuntimeTest {
 static int checks;static void Check(bool v){checks++;if(!v)throw new Exception("server runtime "+checks);}
 static object Call(object o,string key,params object[] a){return o.GetType().GetMethod(key,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,a);}
 static SiteReply Reply(int online=0,int transfers=0,bool active=true){return new SiteReply{ok=true,service="orange-sites",version=1,owner="1",items=new List<HostedSite>{new HostedSite{id=1,host=123,url="s1.sites.pcos",active=active,online=online,transferring=transfers},new HostedSite{id=2,host=456,url="s2.sites.pcos",active=true}}};}
 static void Main(){
 MainSetup();var service=new SiteHostingService();Call(service,"Awake");WorkshopClient.Instance=new WorkshopClient();int requests=0;Dictionary<string,string> values=null;string action=null;Action<string,string> pending=null;
 WorkshopClient.Instance.Handler=(a,v,cb)=>{requests++;action=a;values=v;pending=cb;};
 service.Refresh(null);pending(JsonUtility.ToJson(Reply()),null);Check(service.Hosting(123)&&service.ServerMemory(123,8192)==4096&&service.Demand(123)==20);
 Call(service,"Apply",Reply(3,0),null);Check(service.ServerMemory(123,8192)==4108&&service.Demand(123)>20);
 float idleDemand=service.Demand(123);Call(service,"Apply",Reply(3,2),null);Check(service.ServerMemory(123,8192)==4140&&service.Demand(123)==idleDemand+240);Check(service.ServerMemory(123,32)==32);Check(service.ServerMemory(999,8192)==0);
 var burst=Reply(3,0);burst.items[0].downloads=1;Call(service,"Apply",burst,null);Check(service.Demand(123)>idleDemand&&service.ServerMemory(123,8192)>4108);Call(service,"Apply",burst,null);Check(service.Demand(123)==idleDemand&&service.ServerMemory(123,8192)==4108);
 var shared=Reply();shared.items.Add(new HostedSite{id=3,host=123,active=true});Call(service,"Apply",shared,null);Check(service.ServerMemory(123,8192)==4096);
 service.LeavingWorld=true;service.HostStopped(123);Check(requests==1&&service.Hosting(123));service.LeavingWorld=false;
 SaveManager.RestoringSave=true;service.HostStopped(123);Check(requests==1&&service.Hosting(123));SaveManager.RestoringSave=false;
 service.Refresh(null);Check(action=="stats");service.HostStopped(123);Check(!service.Hosting(123)&&service.Demand(123)==0&&service.ServerMemory(123,8192)==0&&requests==2);pending(JsonUtility.ToJson(Reply(3,2)),null);Check(action=="host_stop"&&values["host"]=="123"&&values["stopped_hosts"]=="123");pending(JsonUtility.ToJson(Reply(0,0,false)),null);Check(!service.Hosting(123)&&service.Hosting(456)&&service.ServerMemory(456,1024)==512);
 service.Refresh(null);Check(values["stopped_hosts"]=="123");pending(null,"offline");service.Refresh(null);Check(values["stopped_hosts"]=="123");pending(JsonUtility.ToJson(Reply(0,0,false)),null);
 Check(SiteHostingService.PublicationCost(new HostedSite{id=1,url="custom.com",active=false,custom_paid=true},"custom.com")==1100);Check(SiteHostingService.PublicationCost(new HostedSite{id=1,url="custom.com",active=true,custom_paid=true},"custom.com")==0);
 Console.WriteLine("PASS: "+checks+" actual hosting runtime checks: Server.exe reservation/load, shared per-PC RAM, visitor/transfer demand, shutdown queue/retry, exit/restore suppression and recurring-domain restart cost");
 }
 static void MainSetup(){global::Main.Instance=new Main{Money=10000};SaveManager.Loader=new TestLoader();SaveManager.Instance=new SaveManager();ServerAccounts.Token="owner";ServerAccounts.LoggedIn=true;Time.unscaledTime=100;}
}
