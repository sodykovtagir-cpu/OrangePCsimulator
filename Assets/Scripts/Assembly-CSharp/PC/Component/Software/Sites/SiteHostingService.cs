using System;
using System.Collections.Generic;
using UnityEngine;
namespace PC.Component.Software.Sites
{
 [Serializable] public sealed class HostedSite {public int id,host,online,visits,bytes,revision;public bool active;public long total,next_bill;public string url,title,description,publication_key;}
 [Serializable] public sealed class SiteReply {public bool ok,available;public string service,error,document,ticket,owner;public int version,price,period,custom_price,charged;public long total,now;public HostedSite item;public List<HostedSite> items;}
 [Serializable] public sealed class SiteHostingLedger {public string owner;public long charged;}
 public sealed class SiteHostingService:MonoBehaviour
 {
  public static SiteHostingService Instance {get;private set;}
  public bool Available {get;private set;}public string Status="Хостинг ещё не проверен";public List<HostedSite> Items=new List<HostedSite>();
  public string World {get {var game=SaveManager.Loader!=null?SaveManager.Loader.GameData:null;if(game==null)return "";if(string.IsNullOrEmpty(game.siteHostingWorld))game.siteHostingWorld=Guid.NewGuid().ToString("N");return game.siteHostingWorld;}}
  string context="";float nextPoll,lastStats;bool busy;readonly Dictionary<int,float> demand=new Dictionary<int,float>();readonly Dictionary<int,int> visits=new Dictionary<int,int>();
  void Awake(){Instance=this;}
  void OnDestroy(){if(Instance==this)Instance=null;}
  public static void Request(string action,Dictionary<string,string> values,Action<SiteReply,string> done)
  {
   var client=WorkshopClient.Ensure();if(client==null){done(null,"Сервис сайтов недоступен");return;}
   client.SiteRequest(action,values,(body,error)=>{SiteReply result=null;if(error==null)try{result=JsonUtility.FromJson<SiteReply>(body);}catch(Exception){error="Неверный ответ сервиса сайтов";}
    if(error==null&&(result==null||result.service!="orange-sites"||result.version!=1||!result.ok))error=result!=null?result.error:"Сервис сайтов недоступен";
    done(error==null?result:null,error);
   });
  }
  void Update(){string active=ServerAccounts.Token+"|"+World;if(context!=active){context=active;Available=false;Items.Clear();demand.Clear();visits.Clear();lastStats=0;nextPoll=0;}if(!ServerAccounts.LoggedIn){Available=false;Items.Clear();demand.Clear();return;}if(!busy&&Time.unscaledTime>=nextPoll&&Main.Instance!=null&&!SaveManager.RestoringSave&&ServerAccounts.LoggedIn&&!string.IsNullOrEmpty(World))Refresh(null);}
  public float Demand(int host){float value;return Available&&Time.unscaledTime-lastStats<45&&demand.TryGetValue(host,out value)?value:0;}
  public void Refresh(Action completed)
  {
   if(busy){completed?.Invoke();return;}string token=ServerAccounts.Token,world=World;if(string.IsNullOrEmpty(world)||!ServerAccounts.LoggedIn){Status="Войди в аккаунт игры";completed?.Invoke();return;}
   busy=true;nextPoll=Time.unscaledTime+20;
   Request("stats",new Dictionary<string,string>{{"world",world}},(reply,error)=>{busy=false;if(token!=ServerAccounts.Token||world!=World){completed?.Invoke();return;}Apply(reply,error);completed?.Invoke();});
  }
  void Apply(SiteReply reply,string error)
  {
   if(error!=null){Available=false;Items.Clear();demand.Clear();Status=error;return;}
   Available=true;Items=reply.items??new List<HostedSite>();demand.Clear();var counts=new Dictionary<int,int>();var online=new Dictionary<int,int>();
   foreach(var site in Items)if(site.active){if(!counts.ContainsKey(site.host)){counts[site.host]=0;online[site.host]=0;}counts[site.host]+=site.visits;online[site.host]+=site.online;if(!demand.ContainsKey(site.host))demand[site.host]=0;demand[site.host]+=20;}
   foreach(var pair in counts){int before;visits.TryGetValue(pair.Key,out before);float rate=lastStats>0?Mathf.Max(0,pair.Value-before)*60/Mathf.Max(1,Time.unscaledTime-lastStats):0;demand[pair.Key]+=online[pair.Key]*35+rate*4;visits[pair.Key]=pair.Value;}
   lastStats=Time.unscaledTime;Status="Подключено. $100 / 30 реальных минут. Начислено: $"+reply.total;
   var game=SaveManager.Loader!=null?SaveManager.Loader.GameData:null;if(game==null||Main.Instance==null)return;
   if(game.siteHostingLedger==null)game.siteHostingLedger=new List<SiteHostingLedger>();var ledger=game.siteHostingLedger.Find(l=>l.owner==reply.owner);if(ledger==null){ledger=new SiteHostingLedger{owner=reply.owner};game.siteHostingLedger.Add(ledger);}
   long due=reply.total-ledger.charged;if(due<=0)return;int money=Main.Instance.Money;long paid=ledger.charged;
   Main.Instance.SetMoney((int)Math.Max(int.MinValue,(long)money-due),true);ledger.charged=reply.total;
   try{if(SaveManager.Instance==null||!SaveManager.Instance.SaveData())throw new InvalidOperationException();Status+=". Списано: $"+due+" (включая оффлайн)";}
   catch(Exception){Main.Instance.SetMoney(money,true);ledger.charged=paid;Status="Не удалось сохранить списание. Начисления сохранены на сервере; повтор при следующем соединении.";}
  }
  public void Publish(string document,string url,int host,Action<string> done)
  {
   if(busy){done("Дождись текущего запроса");return;}
   var doc=SiteDocument.Parse(document);string token=ServerAccounts.Token,world=World;if(!ServerAccounts.LoggedIn||string.IsNullOrEmpty(world)){done("Нужны аккаунт и сохранение игры");return;}
   var existing=Items.Find(item=>item.publication_key==doc.hostingKey);int estimate=(existing!=null&&existing.active?0:100)+(existing==null&&!string.IsNullOrWhiteSpace(url)?1000:0);
   if(Main.Instance==null||Main.Instance.Money<estimate){done("Нужно $100 за первый период (+$1000 за собственный URL при первой публикации)");return;}
   if(SaveManager.Instance==null||!SaveManager.Instance.SaveData()){done("Сначала сохрани игру");return;}
   busy=true;Request("publish",new Dictionary<string,string>{{"world",world},{"key",doc.hostingKey},{"url",url},{"host",host.ToString(System.Globalization.CultureInfo.InvariantCulture)},{"document",document},{"confirm","100/1800/1000"}},(reply,error)=>{busy=false;if(token!=ServerAccounts.Token||world!=World)return;Apply(reply,error);done(error??("Опубликовано: "+reply.item.url+". "+Status));});
  }
  public void Stop(int id,Action<string> done)
  {
   string token=ServerAccounts.Token,world=World;Request("stop",new Dictionary<string,string>{{"world",world},{"id",id.ToString()}},(reply,error)=>{if(token!=ServerAccounts.Token||world!=World)return;Apply(reply,error);done(error??"Хостинг остановлен. Новых начислений не будет.");});
  }
 }
}
