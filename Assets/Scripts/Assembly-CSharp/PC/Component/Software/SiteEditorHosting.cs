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
  [SerializeField] Dropdown hostedSites;
  [SerializeField] Text hostingStats;
  int hostingSeenRevision=-1,hostingActionEpoch;bool stopPending;
  string hostingProjectKey=null,hostingBoundDomain="";
  readonly List<int> hostedIds=new List<int>();
  void OpenHostingPanel(){if(hostingPanel==null)return;hostingPanel.SetActive(true);hostingPanel.transform.SetAsLastSibling();hostingProjectKey=null;RefreshHosting();}
  public void CloseHosting(){if(hostingPanel!=null)hostingPanel.SetActive(false);}
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
    hostedIds.Add(site.id);options.Add(site.url+(site.banned?" — заблокирован":site.unpaid?" — хост не оплачен":site.active?" — работает":" — остановлен"));
    text.Append(site.url).Append(site.banned?" — заблокирован":site.unpaid?" — хост не оплачен":site.active?" — работает":" — остановлен").Append("\nПосещения: ").Append(site.visits).Append(" | Онлайн: ").Append(site.online).Append(" | ПК: ").Append(site.host).Append("\nНачислено: $").Append(site.total);
    if(site.active&&site.next_bill>0)text.Append(" | Следующее (UTC): ").Append(DateTimeOffset.FromUnixTimeSeconds(site.next_bill).UtcDateTime.ToString("yyyy-MM-dd HH:mm"));
    else text.Append(site.unpaid?" | Ожидается оплата":site.banned?" | Заблокирован: "+site.ban_reason:" | Аренда остановлена");
    text.Append("\n\n");
   }
   hostURL.interactable=!stopPending;
   // Do not destroy an in-progress domain edit on every background refresh.
   if(changedProject||hostURL.text==hostingBoundDomain||string.IsNullOrEmpty(hostURL.text))hostURL.SetTextWithoutNotify(current!=null?current.url:"");
   hostingBoundDomain=current!=null?current.url:"";hostingProjectKey=project.Document.hostingKey;
   if(current!=null){text.Append("Текущий проект: ").Append(current.url).Append("\n");text.Append(current.url).Append("/\n");foreach(var page in project.Document.pages)text.Append(current.url).Append(page.path).Append("\n");}
   else text.Append("Новый сайт: введите домен или оставьте URL пустым для бесплатного адреса.\n");
   text.Append("\nАренда: $100 / 30 мин. Домен: +$1000 / 30 мин.");
   hostedSites.ClearOptions();hostedSites.AddOptions(options);hostedSites.SetValueWithoutNotify(Mathf.Max(0,hostedIds.IndexOf(selected)));hostingStats.text=text.ToString();hostingSeenRevision=service.StateRevision;
  }
  public void PublishSite()
  {
   var service=SiteHostingService.Instance;if(service==null||system==null||!system.Ready)return;
   if(stopPending){Report("Дождись подтверждения остановки");return;}int operation=++hostingActionEpoch;
   if(string.IsNullOrEmpty(project.Document.hostingKey))project.Change(d=>d.hostingKey=Guid.NewGuid().ToString("N"));
   string url=hostURL.text.Trim();int host=system.Board!=null?system.Board.Id:0;
   SaveProject(file=>{if(file!=null)StartCoroutine(PublishTransfer(file.content,url,host,editEpoch,ServerAccounts.Token,operation));});
  }
  System.Collections.IEnumerator PublishTransfer(string content,string url,int host,int epoch,string token,int operation)
  {
   if(operation!=hostingActionEpoch)yield break;
   float delay=0;string error=null;
   if(InternetService.Instance==null||!InternetService.Instance.TryTransfer(system.Board,"sites.pcos",Mathf.Max(.01f,System.Text.Encoding.UTF8.GetByteCount(content)/1048576f),out delay,out error)){Report(error??"Нужен игровой интернет");yield break;}
   yield return new WaitForSecondsRealtime(delay);
   if(operation!=hostingActionEpoch||epoch!=editEpoch||token!=ServerAccounts.Token||system==null||!system.Ready)yield break;
   var connection=InternetService.Instance.Status(system.Board);if(connection==null||connection.Quality<=0){Report("Соединение прервано");yield break;}
   SiteHostingService.Instance.Publish(content,url,host,message=>{if(this!=null){Report(message);DrawHosting();}});
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
