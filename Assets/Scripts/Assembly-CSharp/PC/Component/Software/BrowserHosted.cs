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
  int remoteId;string remoteTicket="",remoteSession=Guid.NewGuid().ToString("N");float remoteBeat;bool remoteBusy,keepRemote;
  void LoadCustomResults(string query)
  {
   int generation=localGeneration;
   SiteHostingService.Request("list",new Dictionary<string,string>{{"query",query}},(reply,error)=>{
    if(this==null||generation!=localGeneration||siteNavigationClosed||error!=null||reply.items==null)return;
    foreach(var site in reply.items){if(!site.active)continue;var copy=site;var row=Instantiate(oggleResultPrefab,resultsContainer);var item=row.GetComponent<OggleResultItem>();item.title.text=site.url;item.description.text=site.title+" — "+site.description;row.GetComponent<Button>().onClick.AddListener(()=>{addressBar.text=copy.url;Search();});}
   });
  }
  void CustomOrResults(string query,string url)
  {
   if(query.IndexOf(' ')>=0||query.IndexOf('.')<0){ShowResults(query.ToLowerInvariant());return;}
   int generation=localGeneration;
   SiteHostingService.Request("view",new Dictionary<string,string>{{"url",url}},(reply,error)=>{
    if(this==null||generation!=localGeneration||siteNavigationClosed)return;
    if(error!=null){ShowResults(query.ToLowerInvariant());return;}
    SiteDocument root=null;string route="/";
    try{root=SiteDocument.Parse(reply.document);int slash=url.IndexOf('/');if(slash>=0)route=url.Substring(slash);if(!SitePages.SafeRoute(route))throw new ArgumentException("Invalid page path");SitePages.At(root,route);}
    catch(Exception ex){ShowError(ex.Message);return;}
    string requestedRoute=route;LoadOnline(url,Mathf.Max(.01f,reply.item.bytes/1048576f),()=>RenderHosted(root,requestedRoute,reply.item,reply.ticket));
   });
  }
  void RenderHosted(SiteDocument root,string route,HostedSite item,string ticket)
  {
   keepRemote=remoteId==item.id;ClearPage();keepRemote=false;oggleHome.SetActive(false);
   var page=SitePages.At(root,route);localDocument=page;localNavigationStarted=true;
   remoteId=item.id;remoteTicket=ticket;remoteBeat=Time.unscaledTime;remoteBusy=false;
   string url=item.url+(route=="/"?"":route);addressBar.SetTextWithoutNotify(url);
   var canvas=SiteCanvas.Create(pageContainer);networkPage=canvas.gameObject;canvas.URL=url;
   canvas.Message=message=>system?.ShowMessageBox("Website",message);canvas.Download=DownloadSiteAsset;
   canvas.Redirect=destination=>DeferLocalNavigation(()=>{if(destination.StartsWith("/",StringComparison.Ordinal))RenderHosted(root,destination,item,remoteTicket);else{addressBar.text=destination;Search();}});
   canvas.OpenFile=asset=>DeferLocalNavigation(()=>{keepRemote=true;try{ShowBundledFile(page,0,"",asset,item.url+"/");}finally{keepRemote=false;}});
   canvas.Render(page,false);RememberSite(item.url);Heartbeat();
  }
  static readonly Dictionary<int,Browser> viewportOwners=new Dictionary<int,Browser>();
  public static Vector2 WorkingViewport(int host)
  {
   Browser browser;if(viewportOwners.TryGetValue(host,out browser)&&browser!=null&&!browser.siteNavigationClosed&&browser.isActiveAndEnabled&&browser.pageContainer is RectTransform)
   {var r=((RectTransform)browser.pageContainer).rect;if(r.width>=128&&r.height>=128)return new Vector2(r.width,r.height);}
   return new Vector2(960,600);
  }
  void Update(){if(system!=null&&system.Board!=null&&!siteNavigationClosed)viewportOwners[system.Board.Id]=this;if(remoteId!=0&&Time.unscaledTime>=remoteBeat&&!remoteBusy)Heartbeat();}
  void Heartbeat()
  {
   if(remoteId==0||siteNavigationClosed||system==null||!system.Ready)return;
   var status=InternetService.Instance!=null?InternetService.Instance.Status(system.Board):null;
   if(status==null||status.Quality<=0){ShowError("Нет подключения к интернету");return;}
   int generation=localGeneration,id=remoteId;remoteBusy=true;remoteBeat=Time.unscaledTime+20;
   SiteHostingService.Request("visit",new Dictionary<string,string>{{"id",id.ToString()},{"ticket",remoteTicket},{"session",remoteSession}},(reply,error)=>{
    if(this==null||generation!=localGeneration||id!=remoteId)return;remoteBusy=false;
    if(error!=null){ShowError("Сайт недоступен: "+error);return;}remoteTicket=reply.ticket;
   });
  }
  void ClearCustomContext()
  {
   if(keepRemote){remoteBusy=false;remoteBeat=Time.unscaledTime+1;return;}
   if(remoteId!=0){int id=remoteId;SiteHostingService.Request("leave",new Dictionary<string,string>{{"id",id.ToString()},{"session",remoteSession}},(r,e)=>{});}
   remoteId=0;remoteTicket="";remoteBusy=false;
  }
 }
}
