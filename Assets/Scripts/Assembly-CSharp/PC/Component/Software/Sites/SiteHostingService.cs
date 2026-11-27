using System;
using System.Collections.Generic;
using UnityEngine;
namespace PC.Component.Software.Sites
{
 /// <summary>A publication. `local` sites live in the save, `cloud` sites are on the web host.</summary>
 [Serializable] public sealed class HostedSite
 {
  public int id,host,online,visits,bytes,revision,transferring,downloads;
  public bool active,custom_paid,unpaid,banned,paused,local,reserved,money_known;
  public float cpu_capacity=1000,cpu_load,host_mbps=5;
  public long total,next_bill,paid_seconds,credit,run_seconds,runway;
  public int price,rate,traffic_rate,traffic_seconds,traffic_billed,traffic_cost;
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
 /// player's own disk: the package never leaves the save, but the seconds it
 /// really serves are billed from the same hosting wallet as cloud publications.
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
 /// <summary>
 /// Hosting money model v3: the hosting wallet is a piggy bank filled with the
 /// «Пополнить» button and nothing else. The game balance is never touched by a
 /// background request, so a slow server can never freeze the game while it
 /// writes the save file, and rent is paid:
 ///   * base tariff ($100 / 1800 s, $1100 with a custom domain) while published;
 ///   * a small traffic surcharge ($20 / 1800 s) only for the seconds the site
 ///     really served a visitor or a download.
 /// The whole thing works offline: without a connection the game switches to the
 /// local mode by itself, keeps counting seconds and re-syncs later.
 /// </summary>
 public sealed class SiteHostingService:MonoBehaviour
 {
  public const int StandardRate=100;
  public const int CustomRate=1100;
  public const int TrafficRate=20;
  public const int Period=1800;
  /// <summary>Background sync interval. Every request costs a hitch on mobile, so it is slow.</summary>
  public const float PollInterval=60f;
  public static SiteHostingService Instance {get;private set;}
  public bool Available {get;private set;}
  /// <summary>False when the last request could not reach the server: local mode.</summary>
  public bool Online {get;private set;}=true;
  public string OfflineReason="";
  /// <summary>Failures in a row; the next retry waits OfflineBackoff(attempt).</summary>
  public int FailureStreak {get;private set;}
  public string Status="Хостинг ещё не проверен";
  /// <summary>Money the player moved to the wallet while offline; sent on reconnect.</summary>
  public long PendingTopUp {get;private set;}
  public List<HostedSite> Items=new List<HostedSite>();
  /// <summary>Local publications of the current save, as kept on disk.</summary>
  public List<LocalHostedSite> Locals=new List<LocalHostedSite>();
  /// <summary>Seconds of local hosting served since the last successful report.</summary>
  public long PendingLocalSeconds {get;private set;}
  /// <summary>Known cash left in the hosting wallet. -1 while unknown.</summary>
  public long AvailableMoney {get;private set;}=-1;
  /// <summary>Optimistic figure the panel draws: the wallet minus the bill it grows.</summary>
  public long WalletLeft {get{return AvailableMoney<0?-1:Math.Max(0,AvailableMoney+(lastTotal>0?0:0));}}
  public string World {get {var game=SaveManager.Loader!=null?SaveManager.Loader.GameData:null;if(game==null)return "";if(string.IsNullOrEmpty(game.siteHostingWorld))game.siteHostingWorld=Guid.NewGuid().ToString("N");return game.siteHostingWorld;}}
  public int StateRevision {get;private set;}
  public bool LeavingWorld;
  /// <summary>Retry delay after a failed request: the offline mode gets quieter over time.</summary>
  public static float OfflineBackoff(int failures)
  {
   if(failures<=0)return PollInterval;
   float delay=PollInterval*Mathf.Min(4,1<<Mathf.Min(3,failures));
   return Mathf.Max(PollInterval,delay);
  }
  public static bool IsOfflineError(string error)
  {
   if(string.IsNullOrEmpty(error))return false;
   return error.Contains("недоступен")||error.Contains("не удалось")||error.Contains("Соединение")||error.Contains("таймаут")||error.Contains("Empty reply")||error.Contains("network");
  }
  readonly List<Action> refreshWaiters=new List<Action>();
  readonly Queue<Action> pendingStops=new Queue<Action>();
  string context="";float nextPoll,nextRetry,lastStats;bool busy;long lastTotal;
  readonly Dictionary<int,float> demand=new Dictionary<int,float>();
  readonly Dictionary<int,int> visits=new Dictionary<int,int>();
  readonly Dictionary<int,int> lastDownloads=new Dictionary<int,int>(),downloadBursts=new Dictionary<int,int>();
  readonly HashSet<int> stoppedHosts=new HashSet<int>();
  readonly HashSet<int> queuedHostStops=new HashSet<int>();
  readonly Dictionary<int,SiteHostProfile> hosts=new Dictionary<int,SiteHostProfile>();
  void Awake(){Instance=this;LoadLocals();}
  /// <summary>
  /// A standard publication address has the shape s&lt;number&gt;.sites.pcos.
  /// The server reserves one per publication, and the same shape is generated
  /// locally while offline so the site keeps one stable address.
  /// </summary>
  public static bool IsStandardDomain(string url)
  {
   if(string.IsNullOrEmpty(url)||!url.StartsWith("s",StringComparison.Ordinal)||!url.EndsWith(".sites.pcos",StringComparison.Ordinal))return false;
   for(int i=1;i<url.Length-11;i++)if(url[i]<'0'||url[i]>'9')return false;
   return url.Length>12;
  }
  public static string StandardDomain(string key)
  {
   uint hash=2166136261;if(!string.IsNullOrEmpty(key))foreach(char c in key)hash=(hash^(byte)c)*16777619;
   return "s"+(100000+hash%900000).ToString()+ ".sites.pcos";
  }
  void OnDestroy(){if(Instance==this)Instance=null;}
  void OnApplicationQuit(){LeavingWorld=true;}
  public bool Hosting(int host){return !stoppedHosts.Contains(host)&&Items.Exists(s=>s.host==host&&s.active&&!s.unpaid&&!s.banned);}
  public int ServerMemory(int host,int total){if(!Hosting(host))return 0;int extra=0;foreach(var s in Items)if(s.host==host&&s.active&&!s.unpaid&&!s.banned)extra+=s.online*4+s.transferring*16;int burst;return Math.Min(total,(total+1)/2+extra+(Time.unscaledTime-lastStats<10&&downloadBursts.TryGetValue(host,out burst)?burst*16:0));}
  public float Demand(int host){float value;return !stoppedHosts.Contains(host)&&Available&&Time.unscaledTime-lastStats<45&&demand.TryGetValue(host,out value)?value:0;}
  public void ReportHost(int host,float capacity,float load,float mbps){hosts[host]=new SiteHostProfile{host=host,cpu_capacity=capacity,cpu_load=load,host_mbps=mbps};}
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
  /// <summary>
  /// One request to the sites API. A failure that looks like a network problem
  /// flips the service into the offline mode instead of hammering the server.
  /// </summary>
  public static void Request(string action,Dictionary<string,string> values,Action<SiteReply,string> done)
  {
   var client=WorkshopClient.Ensure();if(client==null){done(null,"Сервис сайтов недоступен");return;}
   client.SiteRequest(action,values,(body,error)=>{
    SiteReply result=null;
    if(error==null)try{result=JsonUtility.FromJson<SiteReply>(body);}catch(Exception){error="Неверный ответ сервиса сайтов";}
    if(error==null&&(result==null||result.service!="orange-sites"||result.version!=1||!result.ok))error=result!=null?result.error:"Сервис сайтов недоступен";
    done(error==null?result:null,error);
   });
  }
  void NoteResult(string error)
  {
   if(error==null){FailureStreak=0;Online=true;OfflineReason="";return;}
   if(!IsOfflineError(error))return;   // a rejected action is not a lost connection
   FailureStreak++;Online=false;OfflineReason=error;StateRevision++;
  }
  void Update(){
   string active=ServerAccounts.Token+"|"+World;
   if(context!=active){context=active;lastOwner="";hosts.Clear();stoppedHosts.Clear();queuedHostStops.Clear();lastDownloads.Clear();downloadBursts.Clear();StateRevision++;Available=false;Items.Clear();demand.Clear();visits.Clear();lastStats=0;nextPoll=0;nextRetry=0;LoadLocals();}
   if(!ServerAccounts.LoggedIn){Available=false;Items.Clear();demand.Clear();return;}
   // Background sync is deliberately rare, and it backs off while the server is
   // unreachable: the game must never stutter because of the hosting service.
   if(busy||Main.Instance==null||SaveManager.RestoringSave||string.IsNullOrEmpty(World))return;
   float now=Time.unscaledTime;
   if(Time.unscaledTime<nextRetry||now<nextPoll)return;
   // After reconnecting, queued money and offline domains settle first: the
   // panel then shows the real wallet balance as soon as the server answers.
   if(Online&&PendingTopUp>0){nextPoll=now+PollInterval;FlushPendingTopUp();return;}
   var unlisted=Locals!=null?Locals.Find(site=>site!=null&&!site.listed):null;
   if(Online&&unlisted!=null){nextPoll=now+PollInterval;RegisterLocal(unlisted.domain,message=>{Status=message;StateRevision++;});return;}
   Refresh(null);
  }
  Dictionary<string,string> Funds(Dictionary<string,string> values)
  {
   var game=SaveManager.Loader!=null?SaveManager.Loader.GameData:null;
   if(game!=null&&Main.Instance!=null){
    // Only the journal is reported: what this save has already paid into the
    // wallet. The game balance itself is never spent here — money moves into
    // the wallet exclusively through TopUp(), which the player triggers.
    long settled=0;var ledger=game.siteHostingLedger;if(ledger!=null)foreach(var row in ledger)if(row.owner==lastOwner)settled=row.charged;
    if(!string.IsNullOrEmpty(lastOwner))values["settled"]=settled.ToString();
    values["balance"]=Math.Max(0,Main.Instance.Money).ToString();
    // The save itself reports how much it moved into hosting. Without it the
    // backend cannot notice a rolled back save, and the same dollars would stay
    // in the game balance and in the wallet at once.
    if(game.siteHostingDeposited>=0)values["deposited"]=game.siteHostingDeposited.ToString();
   }
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
   busy=true;nextPoll=Time.unscaledTime+PollInterval;
   Request("stats",Funds(new Dictionary<string,string>{{"world",world}}),(reply,error)=>{
    if(token==ServerAccounts.Token&&world==World)Apply(reply,error);
    Complete();
   });
  }
  void Complete(Action completed=null)
  {
   busy=false;
   // A failed request waits longer before the next attempt: the game stays
   // offline instead of retrying on every frame.
   nextRetry=Time.unscaledTime+(Online?0:OfflineBackoff(FailureStreak));
   if(Online)nextPoll=Time.unscaledTime+PollInterval;
   var callbacks=refreshWaiters.ToArray();refreshWaiters.Clear();
   completed?.Invoke();foreach(var callback in callbacks)callback();
   if(!busy&&pendingStops.Count>0)pendingStops.Dequeue()();
  }
  void Apply(SiteReply reply,string error)
  {
   NoteResult(error);
   if(error==null&&reply!=null)lastOwner=reply.owner;
   StateRevision++;ApplyCore(reply,error);
  }
  void ApplyCore(SiteReply reply,string error)
  {
   if(error!=null){
    // Keep the last known publications: an offline panel still shows the sites
    // and the served local time instead of an empty screen. CPU demand is
    // dropped, because nothing is confirmed to be online right now.
    demand.Clear();
    Status=Online?"Ошибка сервиса: "+error:"Нет связи с сервером хостинга. Работаем локально: аренда копится, уйдёт при подключении.";
    return;
   }
   Available=true;Items=reply.items??new List<HostedSite>();demand.Clear();var counts=new Dictionary<int,int>();var online=new Dictionary<int,int>();var downloads=new Dictionary<int,int>();downloadBursts.Clear();
   foreach(var site in Items)if(site.active&&!site.unpaid&&!site.banned&&!stoppedHosts.Contains(site.host)){if(!counts.ContainsKey(site.host)){counts[site.host]=0;online[site.host]=0;downloads[site.host]=0;}counts[site.host]+=site.visits;downloads[site.host]+=site.downloads;online[site.host]+=site.online;if(!demand.ContainsKey(site.host))demand[site.host]=0;demand[site.host]+=20+site.transferring*120;}
   foreach(var pair in counts){int before;visits.TryGetValue(pair.Key,out before);float rate=lastStats>0?Mathf.Max(0,pair.Value-before)*60/Mathf.Max(1,Time.unscaledTime-lastStats):0;int previousDownloads;lastDownloads.TryGetValue(pair.Key,out previousDownloads);int burst=lastStats>0?Math.Max(0,downloads[pair.Key]-previousDownloads):0;downloadBursts[pair.Key]=burst;lastDownloads[pair.Key]=downloads[pair.Key];demand[pair.Key]+=online[pair.Key]*35+rate*4+burst*120;visits[pair.Key]=pair.Value;}
   lastStats=Time.unscaledTime;
   AvailableMoney=reply.wallet!=null&&reply.wallet.known?reply.wallet.available:-1;
   var walletGame=SaveManager.Loader!=null?SaveManager.Loader.GameData:null;
   // A save written before the deposits journal starts from what the wallet holds.
   if(walletGame!=null&&walletGame.siteHostingDeposited<0&&reply.wallet!=null&&reply.wallet.known)walletGame.siteHostingDeposited=Math.Max(0,reply.wallet.ceiling);
   // Rent is billed by the wallet, never by the game balance: nothing is deducted
   // here, so a stats answer never has to write the save file.
   lastTotal=reply.total;
   Status=reply.wallet!=null&&reply.wallet.known
    ?"Подключено. В кошельке хостинга: $"+reply.wallet.available+". Аренда $100 / 30 мин, свой домен $1100 / 30 мин (+$1000 один раз), трафик $20 / 30 мин."
    :"Подключено. Кошелёк хостинга пуст: пополни его, иначе сайты стоят на паузе.";
   var game=SaveManager.Loader!=null?SaveManager.Loader.GameData:null;if(game==null)return;
   // The journal mirrors what the backend has billed, so the panel and the save
   // agree. It is bookkeeping only: no money is moved from the game balance.
   if(game.siteHostingLedger==null)game.siteHostingLedger=new List<SiteHostingLedger>();
   var ledger=game.siteHostingLedger.Find(l=>l.owner==reply.owner);
   if(ledger==null){ledger=new SiteHostingLedger{owner=reply.owner,charged=reply.total};game.siteHostingLedger.Add(ledger);}
   else ledger.charged=reply.total;
   // Local served time is reported once; after a successful request its base moves.
   CommitLocalReport();
   if(reply.wallet!=null&&reply.wallet.known)Status+=" Свободно: $"+reply.wallet.available+".";
  }
  void CommitLocalReport()
  {
   var game=SaveManager.Loader!=null?SaveManager.Loader.GameData:null;if(game==null)return;
   if(Locals==null||Locals.Count==0)return;
   long total=0;foreach(var site in Locals)if(site!=null)total+=Math.Max(0,site.served_seconds);
   if(game.siteHostingWallet==null)game.siteHostingWallet=new SiteHostingWallet();
   game.siteHostingWallet.owner=lastOwner;game.siteHostingWallet.servedSeconds=total;game.siteHostingWallet.localSecondsBase=total;
   PendingLocalSeconds=0;
   // The base only moves, the game is not saved here: the next natural save does it.
  }
  HostedSite FindExisting(string publicationKey)
  {
   if(string.IsNullOrEmpty(publicationKey))return null;
   return Items.Find(item=>item!=null&&item.publication_key==publicationKey);
  }
  /// <summary>Price of a publication shown in the panel (never charged by the client).</summary>
  public static int PublicationCost(HostedSite existing,bool customDomain,string requested)
  {
   bool fresh=existing==null||!existing.active||existing.unpaid;
   int rate=customDomain?CustomRate:StandardRate;
   if(fresh)return rate;
   bool wasCustom=existing.custom_paid&&existing.url!="s"+existing.id+".sites.pcos";
   return customDomain&&!wasCustom?1000:0;
  }
  public static int PublicationCost(HostedSite existing,string requested){return PublicationCost(existing,true,requested);}
  /* ------------------------------------------------------------------ wallet */
  /// <summary>
  /// Move any amount from the game balance into the hosting wallet. This is the
  /// only place where the game balance is spent, and it needs the player's
  /// click. Without a connection the amount is queued in the save and sent on
  /// its own as soon as the server is reachable again.
  /// </summary>
  public void TopUp(int amount,Action<string> done)
  {
   if(amount<=0){done("Укажи сумму пополнения");return;}
   if(Main.Instance==null||Main.Instance.Money<amount){done("На балансе нет $"+amount);return;}
   string token=ServerAccounts.Token,world=World;
   if(!ServerAccounts.LoggedIn||string.IsNullOrEmpty(world)){done("Нужны аккаунт и сохранение игры");return;}
   int money=Main.Instance.Money;
   // The journal moves together with the money, so a failure anywhere rolls
   // both back together.
   bool journaled=JournalTopUp(amount);
   Main.Instance.SetMoney(money-amount,true);
   Action rollback=()=>{Main.Instance.SetMoney(money,true);UnjournalTopUp(amount,journaled);SavePending();};
   // A top-up is a background chore of the hosting service, not a reason to
   // freeze the game. Nothing is written to disk here: building a snapshot
   // costs a main-thread pass over the whole save (1,5 s of it, and ~0,4 s even
   // for the bare header of a big save), which the player feels as a hitch on
   // the click. The money and the journal live in the save in memory, the next
   // ordinary save writes them, and the wallet on the server is the authority
   // meanwhile: a save rolled back to before the top-up reports the older
   // journal and the backend takes the same dollars back out of the wallet, so
   // the two never disagree and nothing is duplicated.
   if(!Online)
   {
    QueueTopUp(amount);
    done("Нет интернета: $"+amount+" уйдёт в кошелёк при подключении.");
    return;
   }
   // One top-up at a time: a second click must not start a parallel request.
   busy=true;
   SavePending();
   Request("topup",Funds(new Dictionary<string,string>{{"world",world},{"amount",amount.ToString()}}),(reply,error)=>{
    if(token!=ServerAccounts.Token||world!=World){Complete(()=>done("Аккаунт изменился"));return;}
    if(error!=null){
     if(IsOfflineError(error)){
      // The connection dropped: keep the money and deliver it on reconnect.
      QueueTopUp(amount);Apply(null,error);
      Complete(()=>done("Связь пропала: $"+amount+" уйдёт в кошелёк при подключении."));return;
     }
     // The wallet was not filled: give the money and the journal back together.
     rollback();Apply(null,error);Complete(()=>done("Пополнение не принято: "+error));return;
    }
    Apply(reply,null);Complete(()=>done("Кошелёк хостинга пополнен на $"+amount+". Доступно: $"+(reply.wallet!=null?reply.wallet.available:0)+"."));
   });
  }
  /// <summary>
  /// Journal money moved into hosting before it is written and sent. The server
  /// bounds the wallet by this number, so a save loaded back to before the
  /// top-up takes the money off the server too: the same dollars never exist in
  /// both places. Returns false for a save written before the journal existed;
  /// such a save keeps the old, unbounded wallet.
  /// </summary>
  bool JournalTopUp(long amount)
  {
   var game=SaveManager.Loader!=null?SaveManager.Loader.GameData:null;
   if(game==null||game.siteHostingDeposited<0)return false;
   game.siteHostingDeposited+=amount;return true;
  }
  void UnjournalTopUp(long amount,bool journaled)
  {
   if(!journaled)return;
   var game=SaveManager.Loader!=null?SaveManager.Loader.GameData:null;
   if(game!=null&&game.siteHostingDeposited>=0)game.siteHostingDeposited=Math.Max(0,game.siteHostingDeposited-amount);
  }
  void QueueTopUp(int amount){PendingTopUp=Math.Max(0,PendingTopUp+amount);SavePending();}
  void SavePending()
  {
   var game=SaveManager.Loader!=null?SaveManager.Loader.GameData:null;
   if(game!=null)game.siteHostingPendingTopUp=PendingTopUp;
  }
  /// <summary>Send money queued while offline; it is removed only on an accepted reply.</summary>
  void FlushPendingTopUp()
  {
   long amount=PendingTopUp;if(amount<=0)return;
   string token=ServerAccounts.Token,world=World;
   busy=true;
   Request("topup",Funds(new Dictionary<string,string>{{"world",world},{"amount",amount.ToString()}}),(reply,error)=>{
    if(error==null&&token==ServerAccounts.Token&&world==World)
    {PendingTopUp=Math.Max(0,PendingTopUp-amount);SavePending();Apply(reply,null);}
    else Apply(null,error);
    Complete();
   });
  }
  /// <summary>Last custom domain the player used, offered again in the editor.</summary>
  public string LastCustomDomain
  {
   get{var game=SaveManager.Loader!=null?SaveManager.Loader.GameData:null;return game!=null&&game.siteHostingCustomDomain!=null?game.siteHostingCustomDomain:"";}
  }
  public void RememberCustomDomain(string domain)
  {
   var game=SaveManager.Loader!=null?SaveManager.Loader.GameData:null;
   if(game==null)return;game.siteHostingCustomDomain=domain??"";SavePending();
  }
  /// <summary>
  /// Reserve the standard address of a publication. Online the backend keeps it
  /// on the publication record; offline the same stable address is derived from
  /// the publication key and registered when the connection returns.
  /// </summary>
  public void Reserve(string key,string title,Action<string> done)
  {
   string offline=StandardDomain(key),world=World,token=ServerAccounts.Token;
   if(!Online||!ServerAccounts.LoggedIn||string.IsNullOrEmpty(world)){done(offline);return;}
   Request("reserve",Funds(new Dictionary<string,string>{{"world",world},{"key",key},{"title",title??""}}),(reply,error)=>{
    if(error!=null){Apply(null,error);done(offline);return;}
    if(token==ServerAccounts.Token&&world==World)Apply(reply,null);
    done(reply!=null&&reply.item!=null&&!string.IsNullOrEmpty(reply.item.url)?reply.item.url:offline);
   });
  }
  /* --------------------------------------------------------------- publishing */
  public void Publish(string document,string url,int host,bool customDomain,string diskPath,int diskId,Action<string> done)
  {
   if(busy){done("Дождись текущего запроса");return;}
   var doc=SiteDocument.Parse(document);string token=ServerAccounts.Token,world=World;if(!ServerAccounts.LoggedIn||string.IsNullOrEmpty(world)){done("Нужны аккаунт и сохранение игры");return;}
   if(string.IsNullOrWhiteSpace(url)){done("Укажи домен сайта");return;}
   if(Main.Instance==null){done("Игра не готова");return;}
   // No connection: the game simulates the publication by hosting the site from
   // the save, so it keeps working in-game and is synced on reconnect.
   if(!Online){HostLocal(url,diskPath,diskId,customDomain,"",done);return;}
   // Publishing the package is free; the server bills the rent from the wallet.
   var existing=FindExisting(doc.hostingKey);
   string domain=SiteDocument.NormalizeUrl(url);
   if(existing==null||!existing.active||existing.unpaid)
    if(AvailableMoney==0){done("Кошелёк хостинга пуст. Сначала пополни его кнопкой «Пополнить».");return;}
   if(customDomain&&(existing==null||!(existing.custom_paid))&&AvailableMoney>=0&&AvailableMoney<1000)
   {done("Свой домен стоит $1000 (один раз). В кошельке хостинга только $"+AvailableMoney+".");return;}
   busy=true;
   Request("publish",Funds(new Dictionary<string,string>{{"world",world},{"key",doc.hostingKey},{"url",domain},{"host",host.ToString(System.Globalization.CultureInfo.InvariantCulture)},{"document",document},{"confirm","100/1800/1000"}}),(reply,error)=>{
    if(token!=ServerAccounts.Token||world!=World){Complete(()=>done("Аккаунт или сохранение изменились"));return;}
    if(error!=null){
     if(IsOfflineError(error)){Complete();HostLocal(domain,diskPath,diskId,customDomain,"",done);return;}
     Complete();Refresh(()=>{if(token==ServerAccounts.Token&&world==World){Status=error;StateRevision++;done(error);}});return;
    }
    stoppedHosts.Remove(host);Apply(reply,null);Complete(()=>done("Опубликовано: "+reply.item.url+". "+Status));
   });
  }
  public void Stop(int id,Action<string> done)
  {
   if(id<=0){done("Выбери сайт для остановки");return;}
   if(!Online){done("Нет связи с сервером: остановка уйдёт при подключении.");return;}
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
  /* ---------------------------------------------------------------- local hosting */
  public void LoadLocals()
  {
   Locals=new List<LocalHostedSite>();PendingLocalSeconds=0;
   var game=SaveManager.Loader!=null?SaveManager.Loader.GameData:null;
   if(game!=null&&game.siteHostingLocals!=null)foreach(var site in game.siteHostingLocals)if(site!=null)Locals.Add(site);
   if(game!=null&&game.siteHostingWallet!=null)PendingLocalSeconds=Math.Max(0,game.siteHostingWallet.servedSeconds-game.siteHostingWallet.localSecondsBase);
   PendingTopUp=game!=null?Math.Max(0,game.siteHostingPendingTopUp):0;
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
  /// Host a site straight from the local disk. Works with no connection at all:
  /// the package stays in the save and the domain is reserved later. Only a
  /// custom domain needs the wallet (the one-time fee), the rent is billed for
  /// the seconds the site really serves.
  /// </summary>
  public bool HostLocal(string domain,string diskPath,int diskId,bool customDomain,string title,Action<string> done)
  {
   if(Main.Instance==null){done("Игра не готова");return false;}
   string world=World;if(string.IsNullOrEmpty(world)||!ServerAccounts.LoggedIn){done("Нужны аккаунт и сохранение игры");return false;}
   if(string.IsNullOrWhiteSpace(domain)){done("Укажи домен сайта");return false;}
   string normalized;
   try{normalized=SiteDocument.NormalizeUrl(domain);}catch(ArgumentException ex){done(ex.Message);return false;}
   var site=Local(normalized);
   bool custom=customDomain&&!normalized.StartsWith("local.",StringComparison.Ordinal);
   int fee=(site==null||!site.listed)&&custom?1000:0;
   if(fee>0&&AvailableMoney>=0&&AvailableMoney<fee){done("Свой домен стоит $1000 (один раз). Пополни кошелёк хостинга.");return false;}
   if(site==null){site=new LocalHostedSite{domain=normalized,registered=DateTimeOffset.UtcNow.ToUnixTimeSeconds()};Locals.Add(site);}
   site.diskPath=diskPath??"";site.disk=diskId;site.rate=custom?CustomRate:StandardRate;
   if(!string.IsNullOrEmpty(title))site.title=title;
   // Offline the domain is only reserved locally; it is registered on reconnect.
   site.listed=Online;
   // The locals list belongs to the save in memory and is written by the next
   // ordinary save. Publishing must not freeze the game: a snapshot of a big
   // save costs over a second on the main thread (see TopUp), which the player
   // feels as a hitch on the click. The server bills only the seconds this save
   // reports, so a crash before that save leaves no charged site behind.
   SaveLocals();StateRevision++;
   done(Online
    ? "Сайт "+normalized+" работает из сохранения; аренда $"+site.rate+" / 30 мин за реальное обслуживание."
    : "Оффлайн: сайт "+normalized+" уже работает в игре из сохранения. Домен закрепится и деньги уйдут на сервер при подключении.");
   return true;
  }
  /// <summary>Stop a local publication and forget its served time.</summary>
  public bool StopLocal(string domain,Action<string> done)
  {
   var site=Local(domain);if(site==null){done("Локальный сайт не найден");return false;}
   // Same as publishing: the record is dropped from the save in memory and the
   // next ordinary save writes it, so the stop button answers immediately.
   Locals.Remove(site);SaveLocals();StateRevision++;
   done("Локальный хостинг остановлен: "+domain);
   return true;
  }
  /// <summary>
  /// Register a local publication on the backend so its domain is reserved and
  /// its seconds are billed from the same wallet. Needs the internet; offline it
  /// simply stays pending and is reserved by the next successful request.
  /// </summary>
  public void RegisterLocal(string domain,Action<string> done)
  {
   var site=Local(domain);if(site==null){done("Локальный сайт не найден");return;}
   string token=ServerAccounts.Token,world=World;
   if(!ServerAccounts.LoggedIn||string.IsNullOrEmpty(world)){done("Нужен аккаунт и интернет");return;}
   if(!Online){done("Домен закрепится при подключении к интернету.");return;}
   busy=true;
   Request("local",Funds(new Dictionary<string,string>{{"world",world},{"url",site.domain},{"seconds",Math.Max(0,site.served_seconds).ToString()},{"visits",Math.Max(0,site.visits).ToString()},{"rate",(site.rate==CustomRate?CustomRate:StandardRate).ToString()},{"title",site.title??site.domain},{"confirm","100/1800/1000"}}),(reply,error)=>{
    busy=false;if(error!=null){Apply(null,error);done(error);return;}
    if(token==ServerAccounts.Token&&world==World)Apply(reply,null);
    site.listed=true;SaveLocals();StateRevision++;Complete(()=>done("Домен "+site.domain+" закреплён за сохранением. "+Status));
   });
  }
  /// <summary>Register every pending local publication at once (used after reconnecting).</summary>
  public void RegisterAllLocals(Action<string> done=null)
  {
   if(Locals==null||Locals.Count==0){done?.Invoke("Локальных сайтов нет");return;}
   var site=Locals.Find(s=>s!=null&&!s.listed);
   if(site==null){done?.Invoke("Все локальные домены уже закреплены");return;}
   RegisterLocal(site.domain,done);
  }
  /// <summary>Add one second of served local hosting (called while the site is live).</summary>
  public void AddLocalSecond(string domain)
  {
   var site=Local(domain);if(site==null||!site.listed)return;
   site.served_seconds++;SaveLocals();
  }
  /// <summary>Seconds the current save has served from disk and not synced yet.</summary>
  public long UnsyncedLocalSeconds
  {
   get{long total=0;if(Locals!=null)foreach(var site in Locals)if(site!=null)total+=Math.Max(0,site.served_seconds);return Math.Max(0,total-ReportedLocalSeconds);}
  }
  long ReportedLocalSeconds
  {
   get{var game=SaveManager.Loader!=null?SaveManager.Loader.GameData:null;var wallet=game!=null?game.siteHostingWallet:null;return wallet!=null?Math.Max(0,wallet.localSecondsBase):0;}
  }
  [Serializable] public sealed class SiteHostProfile {public int host;public float cpu_capacity,cpu_load,host_mbps;}
  [Serializable] sealed class HostProfiles {public List<SiteHostProfile> items;}
 }
}
