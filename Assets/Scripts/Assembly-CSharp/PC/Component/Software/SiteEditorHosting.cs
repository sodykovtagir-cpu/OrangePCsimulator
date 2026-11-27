using System;
using System.Collections.Generic;
using PC.Component.Software.Sites;
using PC.Component.Networking;
using UnityEngine;
using UnityEngine.UI;
namespace PC.Component.Software
{
 public sealed partial class SiteEditor
 {
  [SerializeField] GameObject hostingPanel;
  [SerializeField] InputField hostURL;
  [SerializeField] InputField topUpAmount;
  // Dropdown for the address kind: a temporary standard address reserved by the
  // server, or a custom domain typed by the player. Offline hosting is automatic.
  [SerializeField] Dropdown hostDomainKind;
  [SerializeField] Dropdown hostedSites;
  [SerializeField] Text hostingStats;
  int hostingSeenRevision=-1,hostingActionEpoch;bool stopPending;
  /// <summary>0 = standard reserved address, 1 = custom domain.</summary>
  int hostDomainMode;
  string hostingProjectKey=null,hostingBoundDomain="";
  string reservedKey=null,reservedDomain="";bool reserveBusy;
  readonly List<int> hostedIds=new List<int>();
  void OpenHostingPanel(){if(hostingPanel==null)return;hostingPanel.SetActive(true);hostingPanel.transform.SetAsLastSibling();hostingProjectKey=null;if(hostDomainKind!=null)hostDomainMode=(int)Mathf.Clamp(hostDomainKind.value,0,1);RefreshHosting();}
  public void CloseHosting(){if(hostingPanel!=null)hostingPanel.SetActive(false);}
  /// <summary>"2 ч 05 мин" style duration for the remaining paid time.</summary>
  static string Duration(long seconds)
  {
   if(seconds<=0)return "меньше минуты";
   long minutes=seconds/60;
   if(minutes<1)return seconds+" c";
   if(minutes<60)return minutes+" мин";
   return (minutes/60)+" ч "+((minutes%60).ToString("00"))+" мин";
  }
  public void RefreshHosting()
  {
   var service=SiteHostingService.Instance;if(service==null){Report("Сервис хостинга не запущен");return;}
   hostingStats.text=service.Online?"Подключение…":"Нет связи — сайт работает оффлайн…";service.Refresh(()=>{if(this!=null)DrawHosting();});
  }
  void PollHostingView()
  {
   var service=SiteHostingService.Instance;
   // A dropdown change must repaint at once: the standard address is read-only
   // and a custom domain arrives prefilled, without waiting for a server poll.
   if(hostingPanel!=null&&hostingPanel.activeSelf&&service!=null&&project!=null&&
    (hostingSeenRevision!=service.StateRevision||hostingProjectKey!=project.Document.hostingKey||
     (hostDomainKind!=null&&(int)Mathf.Clamp(hostDomainKind.value,0,1)!=hostDomainMode)))DrawHosting();
  }
  /// <summary>
  /// The standard address is reserved by the server and shown read-only in the
  /// domain field. Offline the same stable address is derived from the
  /// publication key, so the site keeps one address until it is registered.
  /// </summary>
  void EnsureStandardDomain(HostedSite current)
  {
   var service=SiteHostingService.Instance;if(service==null)return;
   string key=project.Document.hostingKey;
   if(string.IsNullOrEmpty(key))return;
   // Only a real temporary address counts as the reserved one: a cached custom
   // domain (or a server answer carrying one) must never be shown as standard.
   if(reservedKey==key&&!SiteHostingService.IsStandardDomain(reservedDomain)){reservedKey=null;reservedDomain="";}
   if(current!=null&&SiteHostingService.IsStandardDomain(current.url)){reservedKey=key;reservedDomain=current.url;return;}
   if(!string.IsNullOrEmpty(key)&&reservedKey==key&&!string.IsNullOrEmpty(reservedDomain))return;
   if(reserveBusy)return;
   reserveBusy=true;string token=ServerAccounts.Token,world=service.World;
   service.Reserve(key,project.Document.title,url=>{
    reserveBusy=false;if(this==null||project==null)return;
    if(token!=ServerAccounts.Token||world!=service.World)return;
    if(key!=project.Document.hostingKey)return;
    // A publication that still has a custom domain keeps it until the player
    // publishes with the standard tariff, so the standard address is derived
    // for this key instead of reusing the old custom domain.
    if(!SiteHostingService.IsStandardDomain(url))url=SiteHostingService.StandardDomain(key);
    reservedKey=key;reservedDomain=url;
    if(hostDomainMode==0)hostURL.SetTextWithoutNotify(url);
    hostingSeenRevision=-1;
   });
  }
  void DrawHosting()
  {
   var service=SiteHostingService.Instance;if(service==null)return;
   int selected=hostedSites.value>=0&&hostedSites.value<hostedIds.Count?hostedIds[hostedSites.value]:0;
   var current=service.Items.Find(site=>site.publication_key==project.Document.hostingKey);
   // Whatever address this publication uses today is what "свой домен" must
   // offer later, even after the standard tariff replaces it.
   if(current!=null&&!SiteHostingService.IsStandardDomain(current.url))service.RememberCustomDomain(current.url);
   bool changedProject=hostingProjectKey!=project.Document.hostingKey;
   if(changedProject&&current!=null)selected=current.id;
   if(hostDomainKind!=null)hostDomainMode=(int)Mathf.Clamp(hostDomainKind.value,0,1);
   else hostDomainMode=current!=null&&!SiteHostingService.IsStandardDomain(current.url)?1:0;
   if(hostDomainMode==0)EnsureStandardDomain(current);
   hostedIds.Clear();var options=new List<string>();var text=new System.Text.StringBuilder();
   if(!service.Online)text.Append("ОФФЛАЙН — интернета нет. Сайт работает из сохранения: его можно открыть в браузере игры. Домены и деньги уйдут на сервер, когда связь появится.\n\n");
   text.Append(service.Status).Append("\n\n");
   foreach(var site in service.Items)
   {
    hostedIds.Add(site.id);
    // A stopped publication is stopped, whatever its address is: the standard
    // address may already be released (reserved=false) after it was published.
    string state=site.banned?" — заблокирован"
     :!site.active?(site.reserved?" — адрес зарезервирован":" — остановлен")
     :site.unpaid?(site.pause_reason=="dry"?" — пауза: мало денег":" — хост не оплачен")
     :site.paused?" — пауза":" — работает";
    options.Add(site.url+(site.local?" [оффлайн]":"")+state);
    text.Append(site.url).Append(site.local?" [оффлайн, из сохранения]":"").Append(state)
     .Append("\nПосещения: ").Append(site.visits).Append(" | Онлайн: ").Append(site.online).Append(" | ПК: ").Append(site.host)
     .Append("\nОплачено аренды: $").Append(site.total);
    // A published site pays the base rent all the time, and a little extra only
    // for the seconds a visitor really spent on it.
    if(site.traffic_rate>0)text.Append(" (из них $").Append(site.traffic_cost).Append(" трафик: ").Append(Duration(site.traffic_seconds)).Append(" обслуживания)");
    // Continuous rent: show how much paid time is left instead of a period clock.
    if(site.active&&!site.unpaid&&site.money_known)
     text.Append(" | Хватит ещё на ").Append(Duration(site.runway>0?site.runway:site.next_bill>0?Math.Max(0,site.next_bill-DateTimeOffset.UtcNow.ToUnixTimeSeconds()):0));
    else if(site.active&&!site.unpaid)text.Append(" | Деньги ещё не сверены с сервером");
    if(site.paused&&site.pause_reason=="user")text.Append(" | Пауза владельца");
    if(site.unpaid&&site.pause_reason=="dry")text.Append(" | Пополни хостинг, чтобы продолжить");
    if(site.banned)text.Append(" | Заблокирован: "+site.ban_reason);
    if(!site.active&&!site.reserved)text.Append(" | Аренда остановлена");
    text.Append("\n\n");
   }
   // The standard address is chosen by the server: the field is read-only and
   // shows the reserved domain. A custom domain is typed by the player and is
   // prefilled with the address that was used before.
   hostURL.interactable=hostDomainMode==1&&!stopPending;
   if(hostDomainMode==0)
   {
    string shown=current!=null&&SiteHostingService.IsStandardDomain(current.url)?current.url
     :(reservedKey==project.Document.hostingKey&&SiteHostingService.IsStandardDomain(reservedDomain)?reservedDomain:"");
    hostURL.SetTextWithoutNotify(string.IsNullOrEmpty(shown)?"Резервируем адрес…":shown);
   }
   else if(changedProject||hostURL.text==hostingBoundDomain||string.IsNullOrEmpty(hostURL.text)||SiteHostingService.IsStandardDomain(hostURL.text))
   {
    hostURL.SetTextWithoutNotify(current!=null&&!SiteHostingService.IsStandardDomain(current.url)?current.url:service.LastCustomDomain);
   }
   hostingBoundDomain=current!=null?current.url:"";hostingProjectKey=project.Document.hostingKey;
   if(current!=null){text.Append("Текущий проект: ").Append(current.url).Append("\n");text.Append(current.url).Append("/\n");foreach(var page in project.Document.pages)text.Append(current.url).Append(page.path).Append("\n");}
   else text.Append("Новый сайт: стандартный адрес резервируется сам, свой домен впиши в поле.\n");
   text.Append("\nАренда: $100 / 30 мин за работу сайта + $20 / 30 мин, пока на нём есть посетители и загрузки. Свой домен: +$1000 один раз. Оффлайн сайт работает бесплатно до подключения.\n");
   text.Append("Аренда идёт только из кошелька хостинга: ты сам переводишь туда деньги кнопкой «Пополнить».\n");
   text.Append(hostDomainMode==1
    ? "Свой домен: впиши адрес. Он закрепляется за сохранением и стоит $1000 один раз."
    : "Стандартный адрес: сервер резервирует временный домен вида s<номер>.sites.pcos, поле только для чтения.");
   long available=service.AvailableMoney;
   text.Append("\nКошелёк хостинга: ").Append(available<0?"ещё не сверен с сервером":"$"+available+" свободно");
   if(service.PendingTopUp>0)text.Append(" (+$"+service.PendingTopUp+" ждут подключения)");
   // What that money buys, per the current base tariff.
   if(available>0)text.Append(" (около ").Append(Duration(available*SiteHostingService.Period/SiteHostingService.StandardRate)).Append(" аренды без посетителей)");
   text.Append("\nРежим: ").Append(service.Online?"онлайн — синхронизация раз в минуту":"оффлайн — аренда и домены уйдут при подключении");
   if(service.Locals!=null&&service.Locals.Count>0)
   {
    text.Append("\n\nСайты, работающие из сохранения:\n");
    foreach(var site in service.Locals)
     if(site!=null)text.Append("  ").Append(site.domain).Append(" — ").Append(site.served_seconds).Append(" c обслуживания ≈ $").Append(site.served_seconds*Math.Max(1,site.rate)/SiteHostingService.Period)
      .Append(site.listed?"":" (домен ещё не закреплён — нужен интернет)").Append('\n');
   }
   hostedSites.ClearOptions();hostedSites.AddOptions(options);hostedSites.SetValueWithoutNotify(Mathf.Max(0,hostedIds.IndexOf(selected)));hostingStats.text=text.ToString();hostingSeenRevision=service.StateRevision;
   if(hostDomainKind!=null)
   {
    var labels=new List<string>{"Стандартный адрес","Свой домен"};
    if(hostDomainKind.options==null||hostDomainKind.options.Count!=labels.Count){hostDomainKind.ClearOptions();hostDomainKind.AddOptions(labels);}
    hostDomainKind.SetValueWithoutNotify(hostDomainMode);
   }
  }
  public void PublishSite()
  {
   var service=SiteHostingService.Instance;if(service==null||system==null||!system.Ready)return;
   if(stopPending){Report("Дождись подтверждения остановки");return;}hostingActionEpoch++;
   if(string.IsNullOrEmpty(project.Document.hostingKey))project.Change(d=>d.hostingKey=Guid.NewGuid().ToString("N"));
   hostDomainMode=hostDomainKind!=null?(int)Mathf.Clamp(hostDomainKind.value,0,1):hostDomainMode;
   int host=system.Board!=null?system.Board.Id:0;
   if(hostDomainMode==1)
   {
    // A custom domain must be typed by the player and is remembered for later.
    string custom=hostURL.text.Trim();
    if(string.IsNullOrEmpty(custom)||SiteHostingService.IsStandardDomain(custom)){Report("Впиши свой домен или выбери стандартный адрес");return;}
    service.RememberCustomDomain(custom);
    PublishAddress(custom,host,true);return;
   }
   string key=project.Document.hostingKey;
   var current=service.Items.Find(site=>site.publication_key==key);
   if(current!=null&&SiteHostingService.IsStandardDomain(current.url)){reservedKey=key;reservedDomain=current.url;}
   if(reservedKey==key&&!string.IsNullOrEmpty(reservedDomain)){PublishAddress(reservedDomain,host,false);return;}
   // Reserving is a server request; do it now and publish the answer.
   string token=ServerAccounts.Token,world=service.World;reserveBusy=true;
   hostingStats.text=service.Online?"Резервируем адрес…":"Оффлайн: адрес резервируется локально…";
   service.Reserve(key,project.Document.title,url=>{
    reserveBusy=false;if(this==null)return;
    if(token!=ServerAccounts.Token||world!=service.World||key!=project.Document.hostingKey){Report("Аккаунт или проект изменились");return;}
    reservedKey=key;reservedDomain=url;hostURL.SetTextWithoutNotify(url);
    PublishAddress(url,host,false);
   });
  }
  /// <summary>Save the .osite package and publish it online, or host it from the save offline.</summary>
  void PublishAddress(string url,int host,bool custom)
  {
   var service=SiteHostingService.Instance;if(service==null)return;
   int operation=hostingActionEpoch;
   SaveProject(file=>{
    if(file==null||this==null||system==null)return;
    int disk=-1;string path="";
    if(system.AllStorage!=null)
     for(int i=0;i<system.AllStorage.Count;i++)
     {
      var storage=system.AllStorage[i];if(storage==null||storage.files==null||!storage.files.Contains(file))continue;
      disk=i;path=file.path;break;
     }
    if(!service.Online)
    {
     service.HostLocal(url,path,disk,custom,project.Document.title,message=>{if(this!=null){Report(message);DrawHosting();}});
     if(this!=null)DrawHosting();return;
    }
    StartCoroutine(PublishTransfer(file.content,url,host,editEpoch,ServerAccounts.Token,operation,custom,path,disk));
   });
  }
  System.Collections.IEnumerator PublishTransfer(string content,string url,int host,int epoch,string token,int operation,bool customDomain,string diskPath,int diskId)
  {
   if(operation!=hostingActionEpoch)yield break;
   float delay=0;string error=null;
   // Without a game connection the publication is simulated locally, exactly as
   // if the real internet had dropped: the site keeps working in-game.
   if(InternetService.Instance==null||!InternetService.Instance.TryTransfer(system.Board,"sites.pcos",Mathf.Max(.01f,System.Text.Encoding.UTF8.GetByteCount(content)/1048576f),out delay,out error))
   {
    SiteHostingService.Instance.HostLocal(url,diskPath,diskId,customDomain,project.Document.title,message=>{if(this!=null){Report(message);DrawHosting();}});
    yield break;
   }
   yield return new WaitForSecondsRealtime(delay);
   if(operation!=hostingActionEpoch||epoch!=editEpoch||token!=ServerAccounts.Token||system==null||!system.Ready)yield break;
   var connection=InternetService.Instance.Status(system.Board);if(connection==null||connection.Quality<=0)
   {
    SiteHostingService.Instance.HostLocal(url,diskPath,diskId,customDomain,project.Document.title,message=>{if(this!=null){Report(message);DrawHosting();}});
    yield break;
   }
   SiteHostingService.Instance.Publish(content,url,host,customDomain,diskPath,diskId,message=>{if(this!=null){Report(message);DrawHosting();}});
  }
  /// <summary>
  /// Move any amount the player typed from the game balance into the hosting
  /// wallet. Works offline too: the amount is queued and sent on reconnect.
  /// </summary>
  public void TopUpHosting()
  {
   var service=SiteHostingService.Instance;if(service==null){Report("Сервис хостинга не запущен");return;}
   if(stopPending){Report("Дождись подтверждения остановки");return;}
   int amount=SiteHostingService.StandardRate;
   if(topUpAmount!=null&&(!int.TryParse(topUpAmount.text.Trim(),out amount)||amount<=0))
   {Report("Впиши сумму пополнения в долларах");return;}
   hostingStats.text="Пополнение кошелька на $"+amount+"…";
   service.TopUp(amount,message=>{if(this!=null){DrawHosting();Report(message);}});
  }
  public void StopHostedSite()
  {
   var service=SiteHostingService.Instance;if(service==null){Report("Сервис хостинга не запущен");return;}
   if(stopPending)return;hostingActionEpoch++;stopPending=true;
   string key=project.Document.hostingKey,token=ServerAccounts.Token,world=service.World;
   int id=hostedSites.value>=0&&hostedSites.value<hostedIds.Count?hostedIds[hostedSites.value]:0;
   hostingStats.text="Остановка сайта…";
   Action stop=()=>{
    if(this==null)return;
    if(token!=ServerAccounts.Token||world!=service.World){stopPending=false;Report("Аккаунт или сохранение изменились");return;}
    int target=id;if(target==0){var current=service.Items.Find(site=>site.publication_key==key);if(current!=null)target=current.id;}
    if(target==0){stopPending=false;DrawHosting();Report("Выбери опубликованный сайт для остановки");return;}
    service.Stop(target,message=>{if(this!=null){stopPending=false;DrawHosting();Report(message);}});
   };
   if(id==0)service.Refresh(stop);else stop();
  }
 }
}
