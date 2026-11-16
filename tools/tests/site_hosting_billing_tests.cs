using System;using System.Collections.Generic;using System.Runtime.CompilerServices;using System.Reflection;using PC.Component.Software;using PC.Component.Software.Sites;using UnityEngine;using UnityEngine.UI;
// Money model v2 on the client: nobody goes below zero, local hosting keeps
// working without the internet, and the game never pays the same bill twice.
class HostingBillingTest {
 static int checks;static void Check(bool b,[CallerLineNumber]int line=0){checks++;if(!b)throw new Exception("hosting billing check "+checks+" at line "+line);}
 static void Set(object o,string key,object value){o.GetType().GetField(key,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,value);}
 static object Get(object o,string key){return o.GetType().GetField(key,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);}
 static SiteReply Reply(long total,string owner="1",bool paused=false,long?available=null){
  return new SiteReply{ok=true,service="orange-sites",version=1,owner=owner,total=total,
   wallet=new SiteWallet{known=available.HasValue,ceiling=available??0,spent=total,available=available??0},
   items=new List<HostedSite>{new HostedSite{id=1,url="s1.sites.pcos",publication_key=new string('a',32),active=true,paused=paused,unpaid=paused,rate=100,price=100,money_known=available.HasValue,runway=available.HasValue?1800:0}}};
 }
 static void Main(){
  global::Main.Instance=new Main{Money=250};SaveManager.Loader=new TestLoader();SaveManager.Instance=new SaveManager();ServerAccounts.Token="owner";ServerAccounts.LoggedIn=true;
  var service=new SiteHostingService();typeof(SiteHostingService).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(service,null);
  WorkshopClient.Instance=new WorkshopClient();var calls=new List<string>();Action<string,string> pending=null;Dictionary<string,string> args=null;
  WorkshopClient.Instance.Handler=(action,values,done)=>{calls.Add(action);args=values;pending=done;};
  Action<SiteReply,string> answer=(reply,error)=>{var cb=pending;pending=null;cb(error==null?JsonUtility.ToJson(reply):null,error);};

  // --- the bill is never paid out of thin air: only the money that exists.
  bool refreshed=false;service.Refresh(()=>refreshed=true);answer(Reply(900),null);
  Check(refreshed&&global::Main.Instance.Money==0);            // 250 available, 900 billed
  Check(service.Available&&service.Items.Count==1);
  Check(service.Status.Contains("паузу")||service.Status.Contains("900"));
  // The journal records what the backend billed, so the rest is not charged twice.
  var ledger=SaveManager.Loader.GameData.siteHostingLedger[0];Check(ledger.charged==900);
  global::Main.Instance.Money=500;service.Refresh(null);answer(Reply(900),null);Check(global::Main.Instance.Money==500); // nothing due

  // --- a lower server total (rolled-back save) is accepted, never "refunded" twice.
  service.Refresh(null);answer(Reply(600),null);Check(ledger.charged==600&&global::Main.Instance.Money==500);

  // --- available money is mirrored for the panel.
  service.Refresh(null);answer(Reply(600,available:420),null);Check(service.AvailableMoney==420);
  service.Refresh(null);answer(Reply(600),null);Check(service.AvailableMoney==-1); // still unknown until the first snapshot

  // --- the pause state is visible and a dry site is reported, not hidden.
  service.Refresh(null);answer(Reply(900,paused:true,available:0),null);
  Check(service.Items[0].paused&&service.Items[0].unpaid&&service.Items[0].money_known);
  // $300 became due while only $500 existed, so exactly $300 was paid.
  Check(global::Main.Instance.Money==200&&ledger.charged==900);
  ledger=SaveManager.Loader.GameData.siteHostingLedger[0];

  // --- failed saves roll the money back instead of losing it silently.
  global::Main.Instance.Money=300;SaveManager.Instance.fail=true;
  service.Refresh(null);answer(Reply(1200),null);
  Check(global::Main.Instance.Money==300&&ledger.charged==900&&service.Status.Contains("Не удалось сохранить"));
  SaveManager.Instance.fail=false;
  service.Refresh(null);answer(Reply(900,available:200),null);Check(service.Available);

  // --- local hosting works with no connection at all: no request is made.
  global::Main.Instance.Money=1500;
  int before=calls.Count;bool hosted=service.HostLocal("local.mygame.com","MySite.osite",0,false,"My game",m=>{});
  Check(hosted&&service.Locals.Count==1&&calls.Count==before);
  var local=service.Local("local.mygame.com");Check(local!=null&&local.rate==100);
  Check(SaveManager.Loader.GameData.siteHostingLocals.Count==1);   // persisted in the save
  for(int i=0;i<5;i++)service.AddLocalSecond("local.mygame.com");
  Check(service.Local("local.mygame.com").served_seconds==5&&service.PendingLocalSeconds==5);

  // Served time is reported as an absolute total, so a repeated report is free.
  service.Refresh(null);
  Check(args["locals"].Contains("\"seconds\":5")&&args["locals"].Contains("local.mygame.com"));
  answer(Reply(1200,available:100),null);
  Check(service.PendingLocalSeconds==0);                            // base moved after the answer
  service.AddLocalSecond("local.mygame.com");service.Refresh(null);
  Check(args["locals"].Contains("\"seconds\":6"));                  // running total, not a delta
  answer(Reply(1200,available:100),null);                           // settle the polling request

  // --- a local publication can be registered while online and reserves the domain.
  bool registered=false;service.RegisterLocal("local.mygame.com",m=>registered=true);
  Check(calls[calls.Count-1]=="local"&&args["url"]=="local.mygame.com");
  answer(Reply(1300,available:60),null);Check(registered&&service.Local("local.mygame.com").listed);
  // Without the internet it stays usable locally and is registered later.
  ServerAccounts.LoggedIn=false;bool offlineMessage=false;
  service.RegisterLocal("local.mygame.com",m=>offlineMessage=m.Contains("интернет"));
  Check(offlineMessage&&service.Local("local.mygame.com")!=null);
  ServerAccounts.LoggedIn=true;

  // --- stopping a local site forgets it and its served time in the save.
  Check(service.StopLocal("local.mygame.com",m=>{}));
  Check(service.Locals.Count==0&&service.Local("local.mygame.com")==null);
  Check(SaveManager.Loader.GameData.siteHostingLocals.Count==0);

  // --- top-up moves money into the wallet exactly once.
  global::Main.Instance.Money=2000;SaveManager.Instance.saves=0;
  int requests=calls.Count;service.TopUp(100,m=>{});
  Check(global::Main.Instance.Money==1900&&calls[calls.Count-1]=="topup"&&args["amount"]=="100");
  answer(Reply(1300,available:160),null);
  Check(service.AvailableMoney==160);
  // A refused top-up returns the money to the player.
  int money=global::Main.Instance.Money;service.TopUp(100,m=>{});
  int afterTopUp=global::Main.Instance.Money;Check(afterTopUp==money-100);
  answer(null,"network down");
  // The money comes back to the game balance, and the service keeps the state it
  // had before the refused request instead of dropping the whole publication list.
  Check(global::Main.Instance.Money==money&&service.Available);

  // --- tariffs: a custom domain is $1100 per period, the domain fee is once.
  Check(SiteHostingService.PublicationCost(null,false,"")==100);
  Check(SiteHostingService.PublicationCost(null,true,"my.com")==1100);
  var running=new HostedSite{id=2,active=true,url="my.com",custom_paid=true};
  Check(SiteHostingService.PublicationCost(running,true,"my2.com")==0);
  var standard=new HostedSite{id=3,active=true,url="s3.sites.pcos",custom_paid=false};
  Check(SiteHostingService.PublicationCost(standard,true,"my3.com")==1000);
  Check(SiteHostingService.PublicationCost(standard,false,"")==0);
  Console.WriteLine("PASS: "+checks+" hosting billing checks: zero floor, journal, save rollback, paused state, offline local hosting, served-second totals, domain registration, stop, top-up refund and tariffs");
 }
}
