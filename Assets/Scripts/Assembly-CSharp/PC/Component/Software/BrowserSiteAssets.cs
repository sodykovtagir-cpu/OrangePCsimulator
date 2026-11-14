using System;
using System.Collections;
using System.Collections.Generic;
using PC.Component.Software.Sites;
using PC.Component.Networking;
using UnityEngine;
using UnityEngine.UI;
namespace PC.Component.Software
{
 public partial class Browser
 {
  readonly Dictionary<string,Sprite> hostedIcons=new Dictionary<string,Sprite>();
  HostedSite downloadHost;bool downloading;
  static string SiteDomain(string url){string value=NormalizeUrl(url);int slash=value.IndexOf('/');return slash<0?value:value.Substring(0,slash);}
  Sprite HostedIcon(HostedSite site)
  {
   if(site==null)return fallbackSiteIcon;string domain=SiteDomain(site.url);Sprite cached;
   if(hostedIcons.TryGetValue(domain,out cached))return cached;
   if(string.IsNullOrEmpty(site.icon_data)||site.icon_data.Length>90000)return fallbackSiteIcon;
   Texture2D texture=null;
   try{byte[] bytes=Convert.FromBase64String(site.icon_data);SiteDocument.ValidateImage(bytes,site.icon_media);texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
    if(!texture.LoadImage(bytes)||texture.width>256||texture.height>256)throw new ArgumentException();
    var sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f));
    if(hostedIcons.Count>=128){foreach(var icon in hostedIcons.Values){Destroy(icon.texture);Destroy(icon);}hostedIcons.Clear();}
    hostedIcons[domain]=sprite;return sprite;
   }catch(Exception){if(texture!=null)Destroy(texture);return fallbackSiteIcon;}
  }
  void LoadHistoryMetadata(string url,GameObject row,Text label)
  {
   if(websites!=null)foreach(var site in websites)if(site!=null&&site.page!=null&&NormalizeUrl(site.url)==NormalizeUrl(url)){if(label!=null)label.text=string.IsNullOrWhiteSpace(site.page.websiteName)?url:site.page.websiteName;return;}
   if(RouterWebPages.IsLocalAddress(url))return;
   int generation=localGeneration;
   SiteHostingService.Request("info",new Dictionary<string,string>{{"url",url}},(reply,error)=>{
    if(this==null||row==null||generation!=localGeneration||siteNavigationClosed||error!=null||reply.item==null)return;
    if(label!=null)label.text=(string.IsNullOrWhiteSpace(reply.item.title)?url:reply.item.title)+"\n"+url;
    var raw=row.GetComponentInChildren<RawImage>(true);var sprite=HostedIcon(reply.item);
    if(raw!=null){raw.texture=sprite!=null?sprite.texture:null;raw.enabled=sprite!=null;}
    else{var icon=row.transform.Find("Image");if(icon!=null){var image=icon.GetComponent<Image>();if(image!=null){image.sprite=sprite;image.preserveAspect=true;}}}
   });
  }
  IEnumerator DownloadHostedAsset(SiteAsset asset,HostedSite host,int generation)
  {
   if(downloading)yield break;downloading=true;var progress=system!=null?system.ProgressBar:null;bool transferStarted=false,cancelled=false;string transferTicket=remoteTicket;
   try{
    if(progress!=null)progress.CallProgressBar(Localization.GetText("Downloading")+": "+asset.name,new Color(.3f,1f,.3f,1f),()=>cancelled=true);
    SiteReply reply=null;string error=null;bool complete=false;
    Action<string> check=(phase)=>{complete=false;SiteHostingService.Request("transfer",new Dictionary<string,string>{{"id",host.id.ToString()},{"ticket",transferTicket},{"file",asset.name},{"phase",phase}},(r,e)=>{reply=r;error=e;if(r!=null&&!string.IsNullOrEmpty(r.ticket))transferTicket=r.ticket;complete=true;});};
    check("start");while(!complete){if(cancelled||siteNavigationClosed||generation!=localGeneration)yield break;yield return null;}
    if(error!=null){system?.ShowMessageBox("Website",error);yield break;}
    transferStarted=true;float nextPulse=0;
    float internetDelay=0;string internetError=null;float mb=asset.Bytes().Length/1048576f;
    if(InternetService.Instance==null||!InternetService.Instance.TryTransfer(system.Board,host.url,mb,out internetDelay,out internetError)){system?.ShowMessageBox("Website",internetError??"Нет подключения");yield break;}
    float duration=Mathf.Max(1,SiteTransfer.Delay(mb,internetDelay,reply.item.cpu_capacity,reply.item.cpu_load,reply.item.host_mbps,reply.item.online));
    float elapsed=0;while(elapsed<duration){
     if(cancelled||siteNavigationClosed||generation!=localGeneration||system==null||!system.Ready)yield break;
     var link=InternetService.Instance.Status(system.Board);if(link==null||link.Quality<=0){system.ShowMessageBox("Website","Загрузка прервана: нет соединения");yield break;}
     if(progress!=null)progress.SetProgress(elapsed/Math.Max(.01f,duration));
     if(elapsed>=nextPulse){check("pulse");while(!complete){if(cancelled||siteNavigationClosed||generation!=localGeneration||system==null||!system.Ready)yield break;yield return null;}if(error!=null){system?.ShowMessageBox("Website",error);yield break;}nextPulse=elapsed+10;}

     InternetService.Instance.SignalTraffic(system.Board);float step=Mathf.Min(.25f,duration-elapsed);yield return new WaitForSecondsRealtime(step);elapsed+=step;
    }
    // A stop, ban or exhausted hosting budget during the transfer must also
    // prevent a late save dialog. No execution or arbitrary filesystem writes.
    check("end");while(!complete){if(cancelled||siteNavigationClosed||generation!=localGeneration)yield break;yield return null;}
    if(error!=null){system?.ShowMessageBox("Website",error);yield break;}
    transferStarted=false;if(progress!=null){progress.SetProgress(1);progress.CloseProgressBar();}
    if(generation==localGeneration&&!siteNavigationClosed&&system!=null&&system.Ready)SaveSiteAsset(asset);
   }finally{if(progress!=null)progress.CloseProgressBar();if(transferStarted)SiteHostingService.Request("transfer",new Dictionary<string,string>{{"id",host.id.ToString()},{"ticket",transferTicket},{"file",asset.name},{"phase","cancel"}},(r,e)=>{});downloading=false;}
  }
  void OnDestroy(){foreach(var sprite in hostedIcons.Values){if(sprite!=null){Destroy(sprite.texture);Destroy(sprite);}}hostedIcons.Clear();}
 }
}
