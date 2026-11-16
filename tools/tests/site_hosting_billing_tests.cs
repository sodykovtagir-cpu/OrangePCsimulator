using System;using System.Collections.Generic;using System.Runtime.CompilerServices;using System.Reflection;using PC.Component.Software;using PC.Component.Software.Sites;using UnityEngine;using UnityEngine.UI;
// Money model v3 on the client: the game balance is spent only by an explicit
// top-up, the wallet pays the rent (base tariff + traffic), local hosting keeps
// working with no internet and the service falls back to the local mode by
// itself when the server cannot be reached.
class HostingBillingTest {
 static int checks;static void Check(bool b,[CallerLineNumber]int line=0){checks++;if(!b)throw new Exception("hosting billing check "+checks+" at line "+line);}
 static void Set(object o,string key,object value){o.GetType().GetField(key,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,value);}
 static object Get(object o,string key){return o.GetType().GetField(key,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);}
 static SiteReply Reply(long total,string owner="1",bool paused=false,long?available=null,int rate=100){
  var site=new HostedSite{id=1,url="s1.sites.pcos",publication_key=new string('a',32),active=true,paused=paused,unpaid=paused,rate=rate,price=rate,traffic_rate=20,money_known=available.HasValue,runway=available.HasValue?1800:0};
  return new SiteReply{ok=true,service="orange-sites",version=1,owner=owner,total=total,
   wallet=new SiteWallet{known=available.HasValue,ceiling=available??0,spent=total,available=available??0},
   items=new List<HostedSite>{site}};
 }
 static void Main(){
  global::Main.Instance=new Main{Money=250};SaveManager.Loader=new TestLoader();SaveManager.Instance=new SaveManager();ServerAccounts.Token="owner";ServerAccounts.LoggedIn=true;
  var service=new SiteHostingService();typeof(SiteHostingService).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(service,null);
  WorkshopClient.Instance=new WorkshopClient();var calls=new List<string>();Action<string,string> pending=null;Dictionary<string,string> args=null;
  WorkshopClient.Instance.Handler=(action,values,done)=>{calls.Add(action);args=values;pending=done;};
  Action<SiteReply,string> answer=(reply,error)=>{var cb=pending;pending=null;cb(error==null?JsonUtility.ToJson(reply):null,error);};

  // --- 1. A stats answer never touches the game balance and never writes the
  // save: this is what used to freeze the game on every second of rent.
  bool refreshed=false;service.Refresh(()=>refreshed=true);answer(Reply(900),null);
  Check(refreshed&&service.Available&&service.Items.Count==1);
  Check(global::Main.Instance.Money==250&&SaveManager.Instance.saves==0);
  Check(SaveManager.Loader.GameData.siteHostingLedger[0].charged==900);   // journal only
  Check(service.Status.Contains("Кошелёк")||service.Status.Contains("кошел"));
  // Available wallet money is mirrored for the panel.
  service.Refresh(null);answer(Reply(900,available:420),null);Check(service.AvailableMoney==420);
  service.Refresh(null);answer(Reply(900),null);Check(service.AvailableMoney==-1);

  // --- 2. Top-up is the only place where the game balance is spent, exactly once.
  global::Main.Instance.Money=2000;SaveManager.Instance.saves=0;
  int requests=calls.Count;service.TopUp(100,m=>{});
  Check(global::Main.Instance.Money==1900&&calls[calls.Count-1]=="topup"&&args["amount"]=="100");
  Check(SaveManager.Instance.saves==1);                                   // the spend is saved at once
  answer(Reply(900,available:500),null);Check(service.AvailableMoney==500);
  // A refused top-up returns the money to the player.
  int money=global::Main.Instance.Money;service.TopUp(100,m=>{});
  Check(global::Main.Instance.Money==money-100);
  answer(null,"network down");
  Check(global::Main.Instance.Money==money);                              // refunded
  // While offline no new top-up is attempted at all.
  SaveManager.Instance.saves=0;service.TopUp(100,m=>{});
  Check(global::Main.Instance.Money==money&&SaveManager.Instance.saves==0);

  // --- 3. The offline mode switches itself on and backs off.
  Check(!service.Online);
  float wait=SiteHostingService.OfflineBackoff(1);
  Check(wait>=SiteHostingService.PollInterval&&SiteHostingService.OfflineBackoff(3)>wait);
  Check(SiteHostingService.IsOfflineError("Сервис сайтов недоступен")&&SiteHostingService.IsOfflineError("Empty reply from server"));
  Check(!SiteHostingService.IsOfflineError("URL already taken"));         // a rejection is not offline
  Check(service.Items.Count==1&&service.Status.Contains("локально"));      // the last list is kept
  // Reconnecting restores the online mode.
  service.Refresh(null);answer(Reply(900,available:400),null);Check(service.Online);

  // --- 4. Publishing is refused offline and free online (rent comes from the wallet).
  global::Main.Instance.Money=1500;
  bool hosted=service.HostLocal("local.mygame.com","MySite.osite",0,false,"My game",m=>{});
  Check(hosted&&service.Locals.Count==1);                                 // local works with no request
  var local=service.Local("local.mygame.com");Check(local!=null&&local.rate==100);
  Check(SaveManager.Loader.GameData.siteHostingLocals.Count==1);          // persisted in the save
  for(int i=0;i<5;i++)service.AddLocalSecond("local.mygame.com");
  Check(service.Local("local.mygame.com").served_seconds==5&&service.PendingLocalSeconds==5);
  // Served time is reported as an absolute total, so a repeated report is free.
  service.Refresh(null);
  Check(args["locals"].Contains("\"seconds\":5")&&args["locals"].Contains("local.mygame.com"));
  answer(Reply(900,available:400),null);
  Check(service.PendingLocalSeconds==0);                                  // base moved after the answer
  service.AddLocalSecond("local.mygame.com");service.Refresh(null);
  Check(args["locals"].Contains("\"seconds\":6"));                         // running total, not a delta
  answer(Reply(900,available:400),null);

  // --- 5. A local publication can be registered while online and reserves the domain.
  bool registered=false;service.RegisterLocal("local.mygame.com",m=>registered=true);
  Check(calls[calls.Count-1]=="local"&&args["url"]=="local.mygame.com");
  answer(Reply(1000,available:300),null);Check(registered&&service.Local("local.mygame.com").listed);
  // Without the internet it stays usable locally and is registered later.
  ServerAccounts.LoggedIn=false;bool offlineMessage=false;
  service.RegisterLocal("local.mygame.com",m=>offlineMessage=m.Contains("интернет"));
  Check(offlineMessage&&service.Local("local.mygame.com")!=null);
  ServerAccounts.LoggedIn=true;
  // Stopping a local site forgets it and its served time in the save.
  Check(service.StopLocal("local.mygame.com",m=>{}));
  Check(service.Locals.Count==0&&service.Local("local.mygame.com")==null);
  Check(SaveManager.Loader.GameData.siteHostingLocals.Count==0);

  // --- 6. Prices shown in the panel: base tariff, custom fee once.
  Check(SiteHostingService.PublicationCost(null,false,"")==100);
  Check(SiteHostingService.PublicationCost(null,true,"my.com")==1100);
  var running=new HostedSite{id=2,active=true,url="my.com",custom_paid=true};
  Check(SiteHostingService.PublicationCost(running,true,"my2.com")==0);
  var standard=new HostedSite{id=3,active=true,url="s3.sites.pcos",custom_paid=false};
  Check(SiteHostingService.PublicationCost(standard,true,"my3.com")==1000);
  Check(SiteHostingService.PublicationCost(standard,false,"")==0);

  // --- 7. Publish: no money from the game balance, free when the wallet has cash.
  global::Main.Instance.Money=1500;int publishSaves=SaveManager.Instance.saves;
  var doc=SitePages.New();doc.hostingKey=new string('c',32);
  string message=null;
  service.Publish(doc.ToJson(),"mygame.com",123,true,"",0,m=>message=m);
  Check(message!=null&&message.Contains("кошельк"));   // $300 cannot pay the $1000 domain fee
  Check(global::Main.Instance.Money==1500);                               // nothing deducted locally
  Check(calls[calls.Count-1]!="publish");                                 // refused before any request
  // With enough wallet money the publication goes through and nothing is taken
  // from the game balance: the wallet pays the rent later.
  service.Refresh(null);answer(Reply(1100,available:5000),null);
  message=null;int before=calls.Count;
  service.Publish(doc.ToJson(),"mygame.com",123,true,"",0,m=>message=m);
  Check(calls.Count==before+1&&calls[calls.Count-1]=="publish"&&args["confirm"]=="100/1800/1000");
  Check(global::Main.Instance.Money==1500);
  var publishedReply=Reply(2100,available:4000);publishedReply.item=publishedReply.items[0];publishedReply.item.url="mygame.com";
  answer(publishedReply,null);
  Check(service.AvailableMoney==4000&&message!=null&&message.Contains("Опубликовано"));

  // --- 8. Offline: publishing a new domain is refused, local hosting still works.
  service.Refresh(null);answer(null,"Сервис сайтов недоступен");Check(!service.Online);
  bool refused=false;service.Publish(doc.ToJson(),"mygame.com",123,true,"",0,m=>{refused=true;});
  Check(refused&&service.Locals.Count==0);
  Check(service.HostLocal("local.offline.com","x.osite",0,false,"Offline",m=>{}));
  Check(service.Local("local.offline.com")!=null);
  Console.WriteLine("PASS: "+checks+" hosting billing checks: no game-balance spending on sync, wallet-only top-up with refund, journal-only billing, auto offline mode with backoff, offline local hosting, served-second totals, domain registration, stop, tariffs and publish");
 }
}
