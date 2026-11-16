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
  // Dropdown for the domain kind: a generated s<id>.sites.pcos address, a
  // custom domain typed by the player, or hosting straight from the local disk.
  [SerializeField] Dropdown hostDomainKind;
  [SerializeField] Dropdown hostedSites;
  [SerializeField] Text hostingStats;
  int hostingSeenRevision=-1,hostingActionEpoch;bool stopPending;
  /// <summary>0 = standard generated address, 1 = custom domain, 2 = local hosting.</summary>
  int hostDomainMode;
  string hostingProjectKey=null,hostingBoundDomain="";
  readonly List<int> hostedIds=new List<int>();
  void OpenHostingPanel(){if(hostingPanel==null)return;hostingPanel.SetActive(true);hostingPanel.transform.SetAsLastSibling();hostingProjectKey=null;RefreshHosting();}
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
   hostingStats.text="Подключение…";service.Refresh(()=>{if(this!=null)DrawHosting();});
  }
  void PollHostingView()
  {
   var service=SiteHostingService.Instance;
   if(hostingPanel!=null&&hostingPanel.activeSelf&&service!=null&&project!=null&&(hostingSeenRevision!=service.StateRevision||hostingProjectKey!=project.Document.hostingKey))DrawHosting();
  }
  void DrawHosting()
  {
   var service=SiteHostingService.Instance;if(service==null)return;
   int selected=hostedSites.value>=0&&hostedSites.value<hostedIds.Count?hostedIds[hostedSites.value]:0;
   var current=service.Items.Find(site=>site.publication_key==project.Document.hostingKey);
   bool changedProject=hostingProjectKey!=project.Document.hostingKey;
   if(changedProject&&current!=null)selected=current.id;
   hostedIds.Clear();var options=new List<string>();var text=new System.Text.StringBuilder(service.Status).Append("\n\n");
   foreach(var site in service.Items)
   {
    hostedIds.Add(site.id);
    string state=site.banned?" — заблокирован":site.unpaid?(site.pause_reason=="dry"?" — пауза: мало денег":" — хост не оплачен"):site.paused?" — пауза":" — работает";
    options.Add(site.url+(site.local?" [локально]":"")+state);
    text.Append(site.url).Append(site.local?" [локально в сохранении]":"").Append(state)
     .Append("\nПосещения: ").Append(site.visits).Append(" | Онлайн: ").Append(site.online).Append(" | ПК: ").Append(site.host)
     .Append("\nНачислено: $").Append(site.total);
    // Continuous rent: show how much paid time is left instead of a period clock.
    if(site.active&&!site.unpaid&&site.money_known)
     text.Append(" | Хватит ещё на ").Append(Duration(site.runway>0?site.runway:site.next_bill>0?Math.Max(0,site.next_bill-DateTimeOffset.UtcNow.ToUnixTimeSeconds()):0));
    else if(site.active&&!site.unpaid)text.Append(" | Деньги ещё не сверены с сервером");
    if(site.paused&&site.pause_reason=="user")text.Append(" | Пауза владельца");
    if(site.unpaid&&site.pause_reason=="dry")text.Append(" | Пополни хостинг, чтобы продолжить");
    if(site.banned)text.Append(" | Заблокирован: "+site.ban_reason);
    if(!site.active)text.Append(" | Аренда остановлена");
    text.Append("\n\n");
   }
   hostURL.interactable=!stopPending;
   // Do not destroy an in-progress domain edit on every background refresh.
   if(changedProject||hostURL.text==hostingBoundDomain||string.IsNullOrEmpty(hostURL.text))hostURL.SetTextWithoutNotify(current!=null?current.url:"");
   hostingBoundDomain=current!=null?current.url:"";hostingProjectKey=project.Document.hostingKey;
   if(current!=null){text.Append("Текущий проект: ").Append(current.url).Append("\n");text.Append(current.url).Append("/\n");foreach(var page in project.Document.pages)text.Append(current.url).Append(page.path).Append("\n");}
   else text.Append("Новый сайт: введите домен или оставьте URL пустым для бесплатного адреса.\n");
   text.Append("\nАренда: $100 / 30 мин (посекундно). Свой домен: +$1000 один раз.\n");
   text.Append(hostDomainMode==2
    ? "Хостинг из сохранения: сайт остаётся на твоём диске, публикуется домен, аренда списывается с общего баланса."
    : hostDomainMode==1
     ? "Свой домен: впиши адрес. Он закрепляется за сохранением и стоит $1000 один раз."
     : "Стандартный адрес: домен вида s<номер>.sites.pcos выдаётся сервером.");
   long available=service.AvailableMoney;
   text.Append("\nДеньги хостинга: ").Append(available<0?"ещё не сверены с сервером":"$"+available+" свободно");
   if(service.Locals!=null&&service.Locals.Count>0)
   {
    text.Append("\n\nЛокальные сайты в сохранении:\n");
    foreach(var site in service.Locals)
     if(site!=null)text.Append("  ").Append(site.domain).Append(" — ").Append(site.served_seconds).Append(" c обслуживания")
      .Append(site.listed?"":" (домен ещё не закреплён — нужен интернет)").Append('\n');
   }
   hostedSites.ClearOptions();hostedSites.AddOptions(options);hostedSites.SetValueWithoutNotify(Mathf.Max(0,hostedIds.IndexOf(selected)));hostingStats.text=text.ToString();hostingSeenRevision=service.StateRevision;
   if(hostDomainKind!=null)
   {
    var labels=new List<string>{"Стандартный адрес","Свой домен","Локально в сохранении"};
    if(hostDomainKind.options==null||hostDomainKind.options.Count!=labels.Count){hostDomainKind.ClearOptions();hostDomainKind.AddOptions(labels);}
    hostDomainKind.SetValueWithoutNotify(hostDomainMode);
   }
  }
  public void PublishSite()
  {
   var service=SiteHostingService.Instance;if(service==null||system==null||!system.Ready)return;
   if(stopPending){Report("Дождись подтверждения остановки");return;}int operation=++hostingActionEpoch;
   if(string.IsNullOrEmpty(project.Document.hostingKey))project.Change(d=>d.hostingKey=Guid.NewGuid().ToString("N"));
   hostDomainMode=hostDomainKind!=null?(int)Mathf.Clamp(hostDomainKind.value,0,2):0;
   string url=hostURL.text.Trim();int host=system.Board!=null?system.Board.Id:0;
   if(hostDomainMode==2){PublishLocal(url);return;}
   // A custom domain must be typed by the player; the generated address is chosen
   // by the backend, so the field must stay empty for mode 0.
   if(hostDomainMode==1&&string.IsNullOrEmpty(url)){Report("Впиши свой домен или выбери стандартный адрес");return;}
   if(hostDomainMode==0)url="";
   SaveProject(file=>{if(file!=null)StartCoroutine(PublishTransfer(file.content,url,host,editEpoch,ServerAccounts.Token,operation,hostDomainMode==1));});
  }
  /// <summary>
  /// Local hosting: the .osite stays on the player's disk and is only published
  /// by domain. The rent is still billed from the shared hosting balance.
  /// </summary>
  void PublishLocal(string requested)
  {
   var service=SiteHostingService.Instance;if(service==null)return;
   if(string.IsNullOrEmpty(requested)){Report("Впиши домен локального сайта, например mygame.local");return;}
   string domain;
   try{domain=SiteDocument.NormalizeUrl(requested);}catch(ArgumentException ex){Report(ex.Message);return;}
   int disk=system.FileManager!=null?0:-1;string path="";
   SaveProject(file=>{
    if(file!=null&&system!=null&&system.AllStorage!=null)
     for(int i=0;i<system.AllStorage.Count;i++)
     {
      var storage=system.AllStorage[i];if(storage==null||storage.files==null||!storage.files.Contains(file))continue;
      disk=i;path=file.path;break;
     }
    bool custom=!domain.StartsWith("local.",StringComparison.Ordinal);
    if(service.HostLocal(domain,path,disk,custom,project.Document.title,Report))
    {
     string token=ServerAccounts.Token,world=service.World;
     // The domain is reserved online straight away. Offline it stays pending and
     // is reserved by the next report, so two saves can never both own it.
     service.RegisterLocal(domain,message=>{
      if(this!=null){Report(message);DrawHosting();}
      if(token==ServerAccounts.Token&&world==service.World&&!service.Available)Report("Домен закрепится при подключении к интернету.");
     });
    }
    if(this!=null)DrawHosting();
   });
  }
  System.Collections.IEnumerator PublishTransfer(string content,string url,int host,int epoch,string token,int operation,bool customDomain=false)
  {
   if(operation!=hostingActionEpoch)yield break;
   float delay=0;string error=null;
   if(InternetService.Instance==null||!InternetService.Instance.TryTransfer(system.Board,"sites.pcos",Mathf.Max(.01f,System.Text.Encoding.UTF8.GetByteCount(content)/1048576f),out delay,out error)){Report(error??"Нужен игровой интернет");yield break;}
   yield return new WaitForSecondsRealtime(delay);
   if(operation!=hostingActionEpoch||epoch!=editEpoch||token!=ServerAccounts.Token||system==null||!system.Ready)yield break;
   var connection=InternetService.Instance.Status(system.Board);if(connection==null||connection.Quality<=0){Report("Соединение прервано");yield break;}
   SiteHostingService.Instance.Publish(content,url,host,customDomain,"",system!=null&&system.Board!=null?system.Board.Id:0,message=>{if(this!=null){Report(message);DrawHosting();}});
  }
  /// <summary>Add one $100 period of hosting money from the game balance.</summary>
  public void TopUpHosting()
  {
   var service=SiteHostingService.Instance;if(service==null){Report("Сервис хостинга не запущен");return;}
   if(stopPending){Report("Дождись подтверждения остановки");return;}
   hostingStats.text="Пополнение хостинга…";
   service.TopUp(SiteHostingService.StandardRate,message=>{if(this!=null){DrawHosting();Report(message);}});
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
