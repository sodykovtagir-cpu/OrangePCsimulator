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
  readonly List<int> hostedIds=new List<int>();
  void OpenHostingPanel(){if(hostingPanel==null)return;hostingPanel.SetActive(true);hostingPanel.transform.SetAsLastSibling();RefreshHosting();}
  public void CloseHosting(){if(hostingPanel!=null)hostingPanel.SetActive(false);}
  public void RefreshHosting()
  {
   var service=SiteHostingService.Instance;if(service==null){Report("Сервис хостинга не запущен");return;}
   hostingStats.text="Подключение…";service.Refresh(()=>{if(this!=null)DrawHosting();});
  }
  void DrawHosting()
  {
   var service=SiteHostingService.Instance;if(service==null)return;hostedIds.Clear();var options=new List<string>();var text=new System.Text.StringBuilder(service.Status).Append("\n\n");
   foreach(var site in service.Items){hostedIds.Add(site.id);options.Add(site.url);text.Append(site.url).Append(site.active?" — active":" — stopped").Append("\nVisits: ").Append(site.visits).Append(" | Online: ").Append(site.online).Append(" | Host PC: ").Append(site.host).Append("\nTotal: $").Append(site.total).Append(" | Next (UTC): ").Append(DateTimeOffset.FromUnixTimeSeconds(site.next_bill).UtcDateTime.ToString("yyyy-MM-dd HH:mm")).Append("\n\n");}
   hostedSites.ClearOptions();hostedSites.AddOptions(options);hostingStats.text=text.ToString();
  }
  public void PublishSite()
  {
   var service=SiteHostingService.Instance;if(service==null||system==null||!system.Ready)return;
   if(string.IsNullOrEmpty(project.Document.hostingKey))project.Change(d=>d.hostingKey=Guid.NewGuid().ToString("N"));
   string url=hostURL.text.Trim();int host=system.Board!=null?system.Board.Id:0;
   SaveProject(file=>{if(file!=null)StartCoroutine(PublishTransfer(file.content,url,host,editEpoch,ServerAccounts.Token));});
  }
  System.Collections.IEnumerator PublishTransfer(string content,string url,int host,int epoch,string token)
  {
   float delay=0;string error=null;
   if(InternetService.Instance==null||!InternetService.Instance.TryTransfer(system.Board,"sites.pcos",Mathf.Max(.01f,System.Text.Encoding.UTF8.GetByteCount(content)/1048576f),out delay,out error)){Report(error??"Нужен игровой интернет");yield break;}
   yield return new WaitForSecondsRealtime(delay);
   if(epoch!=editEpoch||token!=ServerAccounts.Token||system==null||!system.Ready)yield break;
   var connection=InternetService.Instance.Status(system.Board);if(connection==null||connection.Quality<=0){Report("Соединение прервано");yield break;}
   SiteHostingService.Instance.Publish(content,url,host,message=>{if(this!=null){Report(message);DrawHosting();}});
  }
  public void StopHostedSite()
  {
   if(hostedSites.value<0||hostedSites.value>=hostedIds.Count)return;
   SiteHostingService.Instance?.Stop(hostedIds[hostedSites.value],message=>{if(this!=null){Report(message);DrawHosting();}});
  }
 }
}
