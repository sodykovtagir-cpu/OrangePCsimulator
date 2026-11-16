using System;
using System.Collections.Generic;
using UnityEngine;
namespace PC.Component.Software.Sites
{
 /// <summary>A publication. `local` sites live in the save, `cloud` sites are on the web host.</summary>
 [Serializable] public sealed class HostedSite
 {
  public int id,host,online,visits,bytes,revision,transferring,downloads;
  public bool active,custom_paid,unpaid,banned,paused,local,money_known;
  public float cpu_capacity=1000,cpu_load,host_mbps=5;
  public long total,next_bill,paid_seconds,credit,run_seconds,runway;
  public int price,rate;
  public string url,title,description,publication_key,icon_data,icon_media,ban_reason,pause_reason,domain;
 }
 [Serializable] public sealed class SiteWallet {public bool known;public long ceiling,spent;public long available;}
 [Serializable] public sealed class SiteReply
 {
  public bool ok,available;
  public string service,error,document,ticket,owner;
  public int version,price,period,custom_price,charged;
  public long total,now;
  public SiteWallet wallet;
  public HostedSite item;
  public List<HostedSite> items;
 }
 [Serializable] public sealed class SiteHostingLedger {public string owner;public long charged;}
 /// <summary>
 /// Local publication record kept in the save. A local site is hosted from the
 /// player's own disk: the package never leaves the save, but the rent is billed
 /// from the same hosting wallet as cloud publications.
 /// </summary>
 [Serializable] public sealed class LocalHostedSite
 {
  public string domain,diskPath,title;
  public int disk,visits,rate=100;
  public long served_seconds,registered;
  public bool listed;
 }
 [Serializable] public sealed class SiteHostingWallet
 {
  public string owner;
  public long totals,servedSeconds;   // what the backend has billed so far
  public long localSecondsBase;       // local served time already reported
 }
 public sealed class SiteHostingService:MonoBehaviour
 {
  public const int StandardRate=100;
  public const int CustomRate=1100;
  public const int Period=1800;
  public static SiteHostingService Instance {get;private set;}
  public bool Available {get;private set;}
  public string Status="Хостинг ещё не проверен";
  public List<HostedSite> Items=new List<HostedSite>();
  /// <summary>Local publications of the current save, as kept on disk.</summary>
  public List<LocalHostedSite> Locals=new List<LocalHostedSite>();
  /// <summary>Seconds of local hosting served since the last successful report.</summary>
  public long PendingLocalSeconds {get;private set;}
  /// <summary>Known cash left for this save on the server. -1 while unknown.</summary>
  public long AvailableMoney {get;private set;}=-1;
  public string World {get {var game=SaveManager.Loader!=null?SaveManager.Loader.GameData:null;if(game==null)return "";if(string.IsNullOrEmpty(game.siteHostingWorld))game.siteHostingWorld=Guid.NewGuid().ToString("N");return game.siteHostingWorld;}}
  public int StateRevision {get;private set;}
  readonly List<Action> refreshWaiters=new List<Action>();
  readonly Queue<Action> pendingStops=new Queue<Action>();
  void Complete(Action completed=null)
  {
   busy=false;var callbacks=refreshWaiters.ToArray();refreshWaiters.Clear();
   completed?.Invoke();foreach(var callback in callbacks)callback();
   if(!busy&&pendingStops.Count>0)pendingStops.Dequeue()();
  }
  string context="";float nextPoll,lastStats;bool busy;readonly Dictionary<int,float> demand=new Dictionary<int,float>();readonly Dictionary<int,int> visits=new Dictionary<int,int>();
  public bool LeavingWorld;
  readonly Dictionary<int,int> lastDownloads=new Dictionary<int,int>(),downloadBursts=new Dictionary<int,int>();
  readonly HashSet<int> stoppedHosts=new HashSet<int>();
  readonly HashSet<int> queuedHostStops=new HashSet<int>();
  void Awake(){Instance=this;LoadLocals();}
  void OnApplicationQuit(){LeavingWorld=true;}
  public bool Hosting(int host){return !stoppedHosts.Contains(host)&&Items.Exists(s=>s.host==host&&s.active&&!s.unpaid&&!s.banned);}
  public int ServerMemory(int host,int total){if(!Hosting(host))return 0;int extra=0;foreach(var s in Items)if(s.host==host&&s.active&&!s.unpaid&&!s.banned)extra+=s.online*4+s.transferring*16;int burst;return Math.Min(total,(total+1)/2+extra+(Time.unscaledTime-lastStats<10&&downloadBursts.TryGetValue(host,out burst)?burst*16:0));}
  public void HostStopped(int host)
  {
   if(LeavingWorld||SaveManager.RestoringSave||string.IsNullOrEmpty(World)||!ServerAccounts.LoggedIn)return;
   if(string.IsNullOrEmpty(context))context=ServerAccounts.Token+"|"+World;
   stoppedHosts.Add(host);demand.Remove(host);StateRevision++;QueueHostStop(host);
  }
  void QueueHostStop(int host)
  {
   if(!queuedHostStops.Add(host))return;string token=ServerAccounts.Token,world=World;
   Action stop=()=>{
    if(token!=ServerAccounts.Token||world!=World){queuedHostStops.Remove(host);Complete();return;}
    busy=true;Request("host_stop",Funds(new Dictionary<string,string>{{"world",world},{"host",host.ToString()}}),(reply,error)=>{
     queuedHostStops.Remove(host);if(token==ServerAccounts.Token&&world==World)Apply(reply,error);Complete();
    });
   };if(busy)pendingStops.Enqueue(stop);else stop();
  }
  void OnDestroy(){if(Instance==this)Instance=null;}
  public static void Request(string action,Dictionary<string,string> values,Action<SiteReply,string> done)
  {
   var client=WorkshopClient.Ensure();if(client==null){done(null,"Сервис сайтов недоступен");return;}
   client.SiteRequest(action,values,(body,error)=>{SiteReply result=null;if(error==null)try{result=JsonUtility.FromJson<SiteReply>(body);}catch(Exception){error="Неверный ответ сервиса сайтов";}
    if(error==null&&(result==null||result.service!="orange-sites"||result.version!=1||!result.ok))error=result!=null?result.error:"Сервис сайтов недоступен";
    done(error==null?result:null,error);
   });
  }
  void Update(){
   string active=ServerAccounts.Token+"|"+World;
   if(context!=active){context=active;lastOwner="";hosts.Clear();stoppedHosts.Clear();queuedHostStops.Clear();lastDownloads.Clear();downloadBursts.Clear();StateRevision++;Available=false;Items.Clear();demand.Clear();visits.Clear();lastStats=0;nextPoll=0;LoadLocals();}
   if(!ServerAccounts.LoggedIn){Available=false;Items.Clear();demand.Clear();return;}
   if(!busy&&Time.unscaledTime>=nextPoll&&Main.Instance!=null&&!SaveManager.RestoringSave&&ServerAccounts.LoggedIn&&!string.IsNullOrEmpty(World))Refresh(null);
  }
  public float Demand(int host){float value;return !stoppedHosts.Contains(host)&&Available&&Time.unscaledTime-lastStats<45&&demand.TryGetValue(host,out value)?value:0;}
  readonly Dictionary<int,SiteHostProfile> hosts=new Dictionary<int,SiteHostProfile>();
  [Serializable] public sealed class SiteHostProfile {public int host;public float cpu_capacity,cpu_load,host_mbps;}
  [Serializable] sealed class HostProfiles {public List<SiteHostProfile> items;}
  public void ReportHost(int host,float capacity,float load,float mbps){hosts[host]=new SiteHostProfile{host=host,cpu_capacity=capacity,cpu_load=load,host_mbps=mbps};}
  Dictionary<string,string> Funds(Dictionary<string,string> values)
  {
   var game=SaveManager.Loader!=null?SaveManager.Loader.GameData:null;
   if(game!=null&&Main.Instance!=null){long settled=0;var ledger=game.siteHostingLedger;if(ledger!=null)foreach(var row in ledger)if(row.owner==lastOwner)settled=row.charged;
    values["balance"]=Math.Max(0,Main.Instance.Money).ToString();
    values["ledgers"]=JsonUtility.ToJson(new LedgerSnapshot{items=ledger??new List<SiteHostingLedger>()});
    if(!string.IsNullOrEmpty(lastOwner))values["settled"]=settled.ToString();}
   values["stopped_hosts"]=string.Join(",",new List<int>(stoppedHosts).ConvertAll(i=>i.ToString()).ToArray());
   var local=LocalReport();if(local!="")values["locals"]=local;
   values["hosts"]=JsonUtility.ToJson(new HostProfiles{items=new List<SiteHostProfile>(hosts.Values)});return values;
  }
  /// <summary>
  /// Served seconds of local hosting for every local publication. The backend
  /// bills them from the same wallet, so an offline site still costs money.
  /// </summary>
  string LocalReport()
  {
   if(Locals==null||Locals.Count==0)return "";
   var report=new List<string>();
   for(int i=0;i<Locals.Count&&i<16;i++)
   {
    var site=Locals[i];if(site==null||string.IsNullOrEmpty(site.domain))continue;
    // The absolute served total is reported: the backend keeps the maximum, so a
    // repeated, retried or lost request can never bill the same second twice.
    long served=Math.Max(0,site.served_seconds);
    report.Add("{\"domain\":\""+Escape(site.domain)+"\",\"seconds\":"+served.ToString()+",\"visits\":"+Math.Max(0,site.visits).ToString()+",\"rate\":"+(site.rate==CustomRate?CustomRate:StandardRate)+",\"title\":\""+Escape(site.title)+"\"}");
   }
   if(report.Count==0)return "";
   return "["+string.Join(",",report.ToArray())+"]";
  }
  static string Escape(string value)
  {
   if(string.IsNullOrEmpty(value))return "";
   var text=new System.Text.StringBuilder(value.Length+8);
   foreach(char c in value)
   {
    if(c=='"'||c=='\\')text.Append('\\').Append(c);
    else if(c<' ')text.Append(' ');
    else text.Append(c);
   }
   return text.ToString();
  }
  long LocalBase(string domain)
  {
   var game=SaveManager.Loader!=null?SaveManager.Loader.GameData:null;var wallet=game!=null?game.siteHostingWallet:null;
   return wallet!=null?wallet.localSecondsBase:0;
  }
  [Serializable] sealed class LedgerSnapshot {public List<SiteHostingLedger> items;}
  string lastOwner="";
  public void Refresh(Action completed)
  {
   if(completed!=null)refreshWaiters.Add(completed);
   // A caller opening the panel must wait for the actual result, not draw an
   // empty list merely because a background stats request is already running.
   if(busy)return;
   string token=ServerAccounts.Token,world=World;
   if(string.IsNullOrEmpty(world)||!ServerAccounts.LoggedIn){Status="Войди в аккаунт игры";Complete();return;}
   if(string.IsNullOrEmpty(context))context=token+"|"+world;
   busy=true;nextPoll=Time.unscaledTime+5;
   Request("stats",Funds(new Dictionary<string,string>{{"world",world}}),(reply,error)=>{
    if(token==ServerAccounts.Token&&world==World)Apply(reply,error);
    Complete();
   });
  }
  void Apply(SiteReply reply,string error)
  {
   if(error==null&&reply!=null)lastOwner=reply.owner;StateRevision++;ApplyCore(reply,error);
  }
  void ApplyCore(SiteReply reply,string error)
  {
   if(error!=null){Available=false;Items.Clear();demand.Clear();Status=error;return;}
   Available=true;Items=reply.items??new List<HostedSite>();demand.Clear();var counts=new Dictionary<int,int>();var online=new Dictionary<int,int>();var downloads=new Dictionary<int,int>();downloadBursts.Clear();
   foreach(var site in Items)if(site.active&&!site.unpaid&&!site.banned&&!stoppedHosts.Contains(site.host)){if(!counts.ContainsKey(site.host)){counts[site.host]=0;online[site.host]=0;downloads[site.host]=0;}counts[site.host]+=site.visits;downloads[site.host]+=site.downloads;online[site.host]+=site.online;if(!demand.ContainsKey(site.host))demand[site.host]=0;demand[site.host]+=20+site.transferring*120;}
   foreach(var pair in counts){int before;visits.TryGetValue(pair.Key,out before);float rate=lastStats>0?Mathf.Max(0,pair.Value-before)*60/Mathf.Max(1,Time.unscaledTime-lastStats):0;int previousDownloads;lastDownloads.TryGetValue(pair.Key,out previousDownloads);int burst=lastStats>0?Math.Max(0,downloads[pair.Key]-previousDownloads):0;downloadBursts[pair.Key]=burst;lastDownloads[pair.Key]=downloads[pair.Key];demand[pair.Key]+=online[pair.Key]*35+rate*4+burst*120;visits[pair.Key]=pair.Value;}
   lastStats=Time.unscaledTime;
   AvailableMoney=reply.wallet!=null&&reply.wallet.known?reply.wallet.available:-1;
   Status="Подключено. Аренда $100 / 30 мин, домен +$1000. Начислено: $"+reply.total;
   var game=SaveManager.Loader!=null?SaveManager.Loader.GameData:null;if(game==null||Main.Instance==null)return;
   if(game.siteHostingLedger==null)game.siteHostingLedger=new List<SiteHostingLedger>();var ledger=game.siteHostingLedger.Find(l=>l.owner==reply.owner);if(ledger==null){ledger=new SiteHostingLedger{owner=reply.owner};game.siteHostingLedger.Add(ledger);}
   // The bill only ever grows, so a lower total means the local ledger is ahead
   // (rolled-back save, lost journal). Trust the backend and pay nothing twice.
   long due=reply.total-ledger.charged;int money=Main.Instance.Money;
   if(due<=0){if(due<0){ledger.charged=reply.total;}}else{
    // Never goes below zero: the rent is limited by the money that actually exists.
    long pay=Math.Min(due,(long)Math.Max(0,money));
    int next=(int)Math.Max(int.MinValue,(long)money-pay);
    Main.Instance.SetMoney(next,true);ledger.charged=reply.total;
    try{if(SaveManager.Instance==null||!SaveManager.Instance.SaveData())throw new InvalidOperationException();}
    catch(Exception){Main.Instance.SetMoney(money,true);ledger.charged=ledger.charged-due;Status="Не удалось сохранить списание. Начисления на сервере; повтор при следующем соединении.";return;}
    if(pay<due)Status+=". Денег хватило только на $"+pay+" из $"+due+": сайт поставлен на паузу.";
    else Status+=". Списано: $"+pay+(due!=pay?"":"");
   }
   // Local served time is reported once; after a successful request its base moves.
   CommitLocalReport();
   if(reply.wallet!=null&&reply.wallet.known)Status+=". Остаток на хостинге: $"+reply.wallet.available;
  }
  void CommitLocalReport()
  {
   var game=SaveManager.Loader!=null?SaveManager.Loader.GameData:null;if(game==null)return;
   if(Locals==null||Locals.Count==0)return;
   long total=0;foreach(var site in Locals)if(site!=null)total+=Math.Max(0,site.served_seconds);
   if(game.siteHostingWallet==null)game.siteHostingWallet=new SiteHostingWallet();
   game.siteHostingWallet.owner=lastOwner;game.siteHostingWallet.totals=0;game.siteHostingWallet.servedSeconds=total;game.siteHostingWallet.localSecondsBase=total;
   PendingLocalSeconds=0;
   try{SaveManager.Instance?.SaveData();}catch(Exception){}
  }
  HostedSite FindExisting(string publicationKey)
  {
   if(string.IsNullOrEmpty(publicationKey))return null;
   return Items.Find(item=>item!=null&&item.publication_key==publicationKey);
  }
  public void Publish(string document,string url,int host,bool customDomain,string diskPath,int diskId,Action<string> done)
  {
   if(busy){done("Дождись текущего запроса");return;}
   var doc=SiteDocument.Parse(document);string token=ServerAccounts.Token,world=World;if(!ServerAccounts.LoggedIn||string.IsNullOrEmpty(world)){done("Нужны аккаунт и сохранение игры");return;}
   if(string.IsNullOrWhiteSpace(url)){done("Укажи домен сайта");return;}
   if(Main.Instance==null){done("Игра не готова");return;}
   // Cloud publication: the verified $100/$1100 per 1800 s tariff, prepaid one period.
   var existing=FindExisting(doc.hostingKey);
   string domain=SiteDocument.NormalizeUrl(url);
   bool sameUrl=existing!=null&&existing.url==domain;
   int cost=PublicationCost(existing,customDomain,url);
   if(Main.Instance.Money<cost){done("Для публикации нужно $"+cost+". Аренда $100 / 30 минут; собственный домен +$1000.");return;}
   if(SaveManager.Instance==null||!SaveManager.Instance.SaveData()){done("Сначала сохрани игру");return;}
   busy=true;
   Request("publish",Funds(new Dictionary<string,string>{{"world",world},{"key",doc.hostingKey},{"url",domain},{"host",host.ToString(System.Globalization.CultureInfo.InvariantCulture)},{"document",document},{"confirm","100/1800/1000"}}),(reply,error)=>{
    if(token!=ServerAccounts.Token||world!=World){Complete(()=>done("Аккаунт или сохранение изменились"));return;}
    if(error!=null){Complete();Refresh(()=>{if(token==ServerAccounts.Token&&world==World){Status=error;StateRevision++;done(error);}});return;}
    stoppedHosts.Remove(host);Apply(reply,null);Complete(()=>done("Опубликовано: "+reply.item.url+". "+Status));
   });
  }
  public void Stop(int id,Action<string> done)
  {
   if(id<=0){done("Выбери сайт для остановки");return;}
   string token=ServerAccounts.Token,world=World;
   Action stop=()=>{
    if(token!=ServerAccounts.Token||world!=World||!ServerAccounts.LoggedIn){done("Аккаунт или сохранение изменились; остановка отменена");if(!busy&&pendingStops.Count>0)pendingStops.Dequeue()();return;}
    busy=true;Request("stop",Funds(new Dictionary<string,string>{{"world",world},{"id",id.ToString()}}),(reply,error)=>{
     if(token!=ServerAccounts.Token||world!=World){Complete(()=>done("Аккаунт или сохранение изменились"));return;}
     Apply(reply,error);
     if(error==null){var stopped=Items.Find(site=>site.id==id);Status=stopped!=null&&!stopped.active?"Сайт "+stopped.url+" остановлен. Он недоступен посетителям; новых начислений не будет.":"Сервер не подтвердил остановку сайта. Обнови статистику.";}
     Complete(()=>done(Status));
    });
   };
   // Serialize writes with stats/publication, so a late stats response cannot
   // overwrite a successful stop with an old active=true snapshot.
   if(busy)pendingStops.Enqueue(stop);else stop();
  }
  /// <summary>Estimated price of a publication or of a domain upgrade.</summary>
  public static int PublicationCost(HostedSite existing,bool customDomain,string requested)
  {
   bool fresh=existing==null||!existing.active||existing.unpaid;
   int rate=customDomain?CustomRate:StandardRate;
   if(fresh)return rate;
   bool wasCustom=existing.custom_paid&&existing.url!="s"+existing.id+".sites.pcos";
   return customDomain&&!wasCustom?1000:0;
  }
  public static int PublicationCost(HostedSite existing,string requested){return PublicationCost(existing,true,requested);}
  /* ---------------------------------------------------------------- local hosting */
  public void LoadLocals()
  {
   Locals=new List<LocalHostedSite>();PendingLocalSeconds=0;
   var game=SaveManager.Loader!=null?SaveManager.Loader.GameData:null;
   if(game!=null&&game.siteHostingLocals!=null)foreach(var site in game.siteHostingLocals)if(site!=null)Locals.Add(site);
   if(game!=null&&game.siteHostingWallet!=null)PendingLocalSeconds=Math.Max(0,game.siteHostingWallet.servedSeconds-game.siteHostingWallet.localSecondsBase);
  }
  void SaveLocals()
  {
   var game=SaveManager.Loader!=null?SaveManager.Loader.GameData:null;if(game==null)return;
   if(game.siteHostingLocals==null)game.siteHostingLocals=new List<LocalHostedSite>();
   game.siteHostingLocals.Clear();game.siteHostingLocals.AddRange(Locals);
   if(game.siteHostingWallet==null)game.siteHostingWallet=new SiteHostingWallet();
   game.siteHostingWallet.servedSeconds=0;foreach(var site in Locals)if(site!=null)game.siteHostingWallet.servedSeconds+=Math.Max(0,site.served_seconds);
   PendingLocalSeconds=Math.Max(0,game.siteHostingWallet.servedSeconds-game.siteHostingWallet.localSecondsBase);
  }
  public LocalHostedSite Local(string domain)
  {
   if(string.IsNullOrEmpty(domain)||Locals==null)return null;
   return Locals.Find(site=>site!=null&&string.Equals(site.domain,domain,StringComparison.OrdinalIgnoreCase));
  }
  /// <summary>
  /// Host a site from the local disk. The package stays in the save; the rent is
  /// billed like a cloud publication, so a local host still costs real money.
  /// Only a whole real second of serving is charged, never wall-clock guesses.
  /// </summary>
  public bool HostLocal(string domain,string diskPath,int diskId,bool customDomain,string title,Action<string> done)
  {
   if(Main.Instance==null){done("Игра не готова");return false;}
   string world=World;if(string.IsNullOrEmpty(world)||!ServerAccounts.LoggedIn){done("Нужны аккаунт и сохранение игры");return false;}
   if(string.IsNullOrWhiteSpace(domain)){done("Укажи домен сайта");return false;}
   string normalized=SiteDocument.NormalizeUrl(domain);
   var site=Local(normalized);
   int rate=customDomain&&!normalized.StartsWith("local.",StringComparison.Ordinal)?CustomRate:StandardRate;
   int cost=site==null?rate:0;
   if(Main.Instance.Money<cost){done("Для локального хостинга нужно $"+cost+". Аренда $100 / 30 минут; свой домен $1000 один раз.");return false;}
   if(site==null){site=new LocalHostedSite{domain=normalized,registered=DateTimeOffset.UtcNow.ToUnixTimeSeconds()};Locals.Add(site);}
   site.diskPath=diskPath??"";site.disk=diskId;site.rate=rate;
   if(!string.IsNullOrEmpty(title))site.title=title;
   site.listed=true;
   // Keep the save before the server knows about the publication, so a crash
   // cannot leave a billed site that the player cannot stop.
   if(SaveManager.Instance==null||!SaveManager.Instance.SaveData()){done("Не удалось сохранить игру");return false;}
   SaveLocals();StateRevision++;
   done("Локальный хостинг включён: "+normalized+". Аренда списывается с общего баланса хостинга.");
   return true;
  }
  /// <summary>Stop a local publication and forget its served time.</summary>
  public bool StopLocal(string domain,Action<string> done)
  {
   var site=Local(domain);if(site==null){done("Локальный сайт не найден");return false;}
   Locals.Remove(site);SaveLocals();StateRevision++;
   if(SaveManager.Instance!=null)SaveManager.Instance.SaveData();
   done("Локальный хостинг остановлен: "+domain);
   return true;
  }
  /// <summary>
  /// Register a local publication on the backend so its domain is reserved and
  /// its rent is accounted with the cloud ones. Requires the game internet.
  /// </summary>
  public void RegisterLocal(string domain,Action<string> done)
  {
   var site=Local(domain);if(site==null){done("Локальный сайт не найден");return;}
   string token=ServerAccounts.Token,world=World;if(!ServerAccounts.LoggedIn||string.IsNullOrEmpty(world)){done("Нужен аккаунт и интернет");return;}
   busy=true;
   Request("local",Funds(new Dictionary<string,string>{{"world",world},{"url",site.domain},{"seconds",Math.Max(0,site.served_seconds).ToString()},{"visits",Math.Max(0,site.visits).ToString()},{"rate",(site.rate==CustomRate?CustomRate:StandardRate).ToString()},{"title",site.title??site.domain},{"confirm","100/1800/1000"}}),(reply,error)=>{
    busy=false;if(error!=null){Refresh(null);done(error);return;}
    if(token==ServerAccounts.Token&&world==World)Apply(reply,null);
    site.listed=true;SaveLocals();StateRevision++;Complete(()=>done("Домен "+site.domain+" закреплён за сохранением. "+Status));
   });
  }
  /// <summary>Add one second of served local hosting (called while the site is live).</summary>
  public void AddLocalSecond(string domain)
  {
   var site=Local(domain);if(site==null||!site.listed)return;
   site.served_seconds++;SaveLocals();
  }
  /// <summary>Move money from the game balance into the hosting wallet.</summary>
  public void TopUp(int amount,Action<string> done)
  {
   if(amount<=0){done("Укажи сумму пополнения");return;}
   if(Main.Instance==null||Main.Instance.Money<amount){done("На балансе нет $"+amount);return;}
   string token=ServerAccounts.Token,world=World;
   int money=Main.Instance.Money;
   Main.Instance.SetMoney(money-amount,true);
   if(SaveManager.Instance==null||!SaveManager.Instance.SaveData()){Main.Instance.SetMoney(money,true);done("Не удалось сохранить игру");return;}
   busy=true;
   Request("topup",Funds(new Dictionary<string,string>{{"world",world},{"amount",amount.ToString()}}),(reply,error)=>{
    if(token!=ServerAccounts.Token||world!=World){Complete(()=>done("Аккаунт изменился"));return;}
    if(error!=null){Main.Instance.SetMoney(money,true);SaveManager.Instance?.SaveData();Complete(()=>done("Пополнение не принято: "+error));return;}
    Apply(reply,null);Complete(()=>done("Хостинг пополнен на $"+amount+". "+Status));
   });
  }
 }
}
