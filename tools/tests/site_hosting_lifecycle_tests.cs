using System;using System.Collections.Generic;using System.Reflection;using PC.Component.Software;using PC.Component.Software.Sites;using UnityEngine;using UnityEngine.UI;
class LifecycleTest {
 static int checks;static void Check(bool b){checks++;if(!b)throw new Exception("hosting lifecycle check "+checks);}
 static void Set(object o,string key,object value){o.GetType().GetField(key,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,value);}
 static void Call(object o,string key){o.GetType().GetMethod(key,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).Invoke(o,null);}
 static SiteReply Reply(bool stopped=false){return new SiteReply{ok=true,service="orange-sites",version=1,owner="1",total=0,items=new List<HostedSite>{new HostedSite{id=1,url="other.com",publication_key=new string('b',32),active=true,custom_paid=true},new HostedSite{id=2,url="current.com",publication_key=new string('a',32),active=!stopped,custom_paid=true,next_bill=stopped?0:2000}}};}
 static void Main(){
 global::Main.Instance=new Main{Money=5000};SaveManager.Loader=new TestLoader();SaveManager.Instance=new SaveManager();ServerAccounts.Token="owner";ServerAccounts.LoggedIn=true;
 var service=new SiteHostingService();Call(service,"Awake");WorkshopClient.Instance=new WorkshopClient();var calls=new List<string>();Action<string,string> pending=null;Dictionary<string,string> args=null;
 WorkshopClient.Instance.Handler=(action,values,done)=>{Check(pending==null);calls.Add(action);args=values;pending=done;};
 Action<SiteReply> answer=reply=>{var cb=pending;pending=null;cb(JsonUtility.ToJson(reply),null);};
 bool a=false,b=false;service.Refresh(()=>a=true);service.Refresh(()=>b=true);Check(calls.Count==1&&!a&&!b);answer(Reply());Check(a&&b&&service.Items.Count==2);
 var editor=new SiteEditor();var doc=SitePages.New();doc.hostingKey=new string('a',32);doc.pages.Add(new SitePage{path="/about"});Set(editor,"project",new SiteHistory(doc));var dropdown=new Dropdown();var url=new InputField();var stats=new Text();Set(editor,"hostedSites",dropdown);Set(editor,"hostURL",url);Set(editor,"hostingStats",stats);Call(editor,"DrawHosting");Check(dropdown.value==1&&url.text=="current.com"&&url.interactable);
 url.text="candidate.com";service.Refresh(null);answer(Reply());Call(editor,"DrawHosting");Check(url.text=="candidate.com"); // polling must not destroy a typed domain
 service.Refresh(null);Call(editor,"StopHostedSite");Check(calls[calls.Count-1]=="stats"&&calls.Count==3); // stop is queued behind stats
 answer(Reply());Check(calls[calls.Count-1]=="stop"&&args["id"]=="2");bool afterStop=false;service.Refresh(()=>afterStop=true);Check(!afterStop);answer(Reply(true));Check(afterStop&&!service.Items[1].active&&service.Items[0].active);Check(stats.text.Contains("Аренда остановлена")&&service.Status.Contains("остановлен"));
 // A stopped publication whose standard address was already released is not
 // "running": the list must say so at once, without waiting for a repaint.
 Call(editor,"DrawHosting");Check(stats.text.Contains("— остановлен")&&dropdown.options[dropdown.value].text.Contains("остановлен"));Check(service.Demand(0)==20); // only the other active site
 // A confirmed stop never turns back into active=true due to a late stats reply:
 // there can be no outstanding earlier stats request after the stop is sent.
 Check(pending==null);Check(SiteHostingService.PublicationCost(service.Items[1],"candidate.com")==1100);
 Check(SiteHostingService.PublicationCost(new HostedSite{id=3,active=true,url="s3.sites.pcos",custom_paid=false},"custom.com")==1000);
 Check(SiteHostingService.PublicationCost(new HostedSite{id=3,active=true,url="custom.com",custom_paid=true},"another.com")==0);
 // Renaming publishes the same key; no client-side immutable-domain rejection.
 service.Publish(doc.ToJson(),"candidate.com",123,true,"pass",123,message=>{});Check(calls[calls.Count-1]=="publish"&&args["key"]==doc.hostingKey&&args["url"]=="candidate.com");var renamed=Reply();renamed.items[1].url="candidate.com";renamed.item=renamed.items[1];answer(renamed);Check(service.Items[1].url=="candidate.com"&&service.Items[1].id==2);
 // A stop click with an empty UI list waits for stats and resolves by the
 // current project's stable publication key, never silently returns.
 var emptyEditor=new SiteEditor();Set(emptyEditor,"project",new SiteHistory(doc));Set(emptyEditor,"hostedSites",new Dropdown());Set(emptyEditor,"hostURL",new InputField());Set(emptyEditor,"hostingStats",new Text());Call(emptyEditor,"StopHostedSite");Check(calls[calls.Count-1]=="stats");answer(renamed);Check(calls[calls.Count-1]=="stop"&&args["id"]=="2");answer(Reply(true));
 // Failed requests must not claim a successful stop, and a lost connection
 // switches the service to the local mode instead of retrying on every frame.
 string error=null;service.Stop(2,message=>error=message);var failed=pending;pending=null;failed(null,"network down");
 Check(error.Contains("связи")||error=="network down");
 Check(service.Status.Contains("локально"));
 // Reconnecting restores the online mode and the real publications.
 service.Refresh(null);answer(Reply());Check(service.Online);
 // Account changes cancel queued writes rather than sending the new account
 // a stop for a publication selected by the old owner.
 service.Refresh(null);int before=calls.Count;string cancelled=null;service.Stop(2,message=>cancelled=message);ServerAccounts.Token="different";answer(Reply());Check(calls.Count==before&&cancelled.Contains("отменена"));
 // Choosing the standard tariff must show the reserved s<number>.sites.pcos, even
 // when this save already published the site at a custom domain.
 ServerAccounts.Token="owner";ServerAccounts.LoggedIn=true;service.Refresh(null);answer(Reply());Check(service.Online&&service.Items.Count==2);
 var stdEditor=new SiteEditor();Set(stdEditor,"project",new SiteHistory(doc));var kind=new Dropdown();kind.value=0;var stdUrl=new InputField();stdUrl.interactable=true;Set(stdEditor,"hostDomainKind",kind);Set(stdEditor,"hostedSites",new Dropdown());Set(stdEditor,"hostURL",stdUrl);Set(stdEditor,"hostingStats",new Text());
 Call(stdEditor,"DrawHosting");Check(calls[calls.Count-1]=="reserve"&&args["key"]==doc.hostingKey);
 var reservedReply=Reply();reservedReply.items[1].url="s222222.sites.pcos";reservedReply.items[1].reserved=true;reservedReply.item=reservedReply.items[1];answer(reservedReply);
 Check(stdUrl.text=="s222222.sites.pcos"&&!stdUrl.interactable);
 Call(stdEditor,"DrawHosting");Check(stdUrl.text=="s222222.sites.pcos"); // the old custom domain never comes back
 // A server that still answers with the custom domain must not be trusted.
 var doc2=SitePages.New();doc2.hostingKey=new string('c',32);
 var staleEditor=new SiteEditor();Set(staleEditor,"project",new SiteHistory(doc2));var staleKind=new Dropdown();staleKind.value=0;var staleUrl=new InputField();staleUrl.interactable=true;Set(staleEditor,"hostDomainKind",staleKind);Set(staleEditor,"hostedSites",new Dropdown());Set(staleEditor,"hostURL",staleUrl);Set(staleEditor,"hostingStats",new Text());
 var customReply=Reply();customReply.items[1].publication_key=doc2.hostingKey;customReply.items[1].url="old.example.com";
 service.Refresh(null);answer(customReply);
 Call(staleEditor,"DrawHosting");Check(calls[calls.Count-1]=="reserve");
 answer(Reply());
 Check(SiteHostingService.IsStandardDomain(staleUrl.text)&&staleUrl.text==SiteHostingService.StandardDomain(doc2.hostingKey)&&!staleUrl.interactable);
 Console.WriteLine("PASS: "+checks+" asynchronous hosting lifecycle checks: refresh coalescing, queued stop, correct project selection, no stale stats reactivation, stop during loading, explicit failure, rename/key preservation, typed-domain preservation, tariff, standard-address reservation and account cancellation");
 }
}
