using System;
using System.Collections.Generic;
using PC.Component.Software.Sites;
using UnityEngine;
using UnityEngine.UI;
namespace PC.Component.Software
{
 public sealed partial class SiteEditor
 {
  [SerializeField] RectTransform pageTabs, actionEditor;
  [SerializeField] RectTransform[] actionTail;
  [SerializeField] InputField pagePath, contextName;
  [SerializeField] GameObject hierarchyMenu;
  [SerializeField] RectTransform contextBox;
  [SerializeField] Dropdown backgroundDropdown;
  string pageRoute="/",contextId="";float actionOriginalHeight;Vector2[] tailPositions;List<string> backgroundValues=new List<string>();
  SiteDocument NewPageDocument(){var size=Browser.WorkingViewport(system!=null&&system.Board!=null?system.Board.Id:0);return SitePages.New(size.x,size.y);}
  SiteDocument EditingDocument { get { return SitePages.At(project.Document,pageRoute); } }
  System.Collections.IEnumerator PreviewRoute(string route,int epoch)
  {
   yield return null;if(this==null||epoch!=editEpoch||!preview)yield break;
   try{SitePages.At(project.Document,route);editEpoch++;pageRoute=route;selected="";Redraw();}catch(Exception ex){Report(ex.Message);}
  }
  void RefreshPages()
  {
   if(pageTabs==null)return;ClearRows(pageTabs);var routes=new List<string>{"/"};foreach(var p in project.Document.pages)routes.Add(p.path);
   for(int i=0;i<routes.Count;i++)
   {
    string route=routes[i];var row=Instantiate(rowTemplate,pageTabs);row.gameObject.SetActive(true);var r=(RectTransform)row.transform;
    r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(i*112,0);r.sizeDelta=new Vector2(108,28);
    row.GetComponentInChildren<Text>().text=route;row.GetComponent<Image>().color=route==pageRoute?new Color(1,.72f,.35f):new Color(.83f,.83f,.83f);
    row.onClick.AddListener(()=>{pagePath.SetTextWithoutNotify(route);editEpoch++;pageRoute=route;selected="";selectedAction=-1;inspector.anchoredPosition=Vector2.zero;Redraw();});
   }
   pageTabs.sizeDelta=new Vector2(routes.Count*112,28);
  }
  public void AddPage()
  {
   string route=(pagePath.text??"").Trim().ToLowerInvariant();if(!route.StartsWith("/"))route="/"+route;
   try { if(!SitePages.SafeRoute(route)||route=="/")throw new ArgumentException("Введите путь, например /about");
    var fresh=NewPageDocument();project.Change(d=>{if(d.pages.Exists(p=>p.path==route))throw new ArgumentException("Такая страница уже есть");d.pages.Add(new SitePage{path=route,title=route.Substring(1),width=fresh.width,height=fresh.height});});
    editEpoch++;pageRoute=route;selected="";Redraw();
   }catch(Exception ex){Report(ex.Message);}
  }
  public void RenamePage()
  {
   string path=(pagePath.text??"").Trim().ToLowerInvariant();if(!path.StartsWith("/"))path="/"+path;
   try{project.Change(root=>SitePages.Rename(root,pageRoute,path));editEpoch++;pageRoute=path;Redraw();Report("Путь страницы изменён; ссылки GoToPage обновлены. Проверь Lua с переменными путями.");}catch(Exception ex){Report(ex.Message);}
  }
  public void DeletePage()
  {
   if(pageRoute=="/"){Report("Главную страницу удалить нельзя");return;}
   editEpoch++;string route=pageRoute;pageRoute="/";selected="";try{project.Change(d=>d.pages.RemoveAll(p=>p.path==route));Redraw();}catch(Exception ex){Report(ex.Message);}
  }
  public void OpenHierarchyMenu(string id,Vector2 point,Camera camera=null)
  {
   if(EditingDocument.Find(id)==null||hierarchyMenu==null)return;view.Select(id);contextId=id;contextName.SetTextWithoutNotify(EditingDocument.Find(id).name);
   hierarchyMenu.SetActive(true);hierarchyMenu.transform.SetAsLastSibling();Vector2 local;
   var parent=(RectTransform)hierarchyMenu.transform;
   if(RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,point,camera,out local))
   {float x=local.x+parent.rect.width*parent.pivot.x,y=-local.y+parent.rect.height*(1-parent.pivot.y);contextBox.anchoredPosition=new Vector2(Mathf.Clamp(x,0,Mathf.Max(0,parent.rect.width-contextBox.sizeDelta.x)),-Mathf.Clamp(y,0,Mathf.Max(0,parent.rect.height-contextBox.sizeDelta.y)));}
  }
  public void CloseHierarchyMenu(){if(hierarchyMenu!=null)hierarchyMenu.SetActive(false);}
  public void ContextRename(){string id=contextId,name=contextName.text;CloseHierarchyMenu();Change(d=>{var e=d.Find(id);if(e!=null)e.name=name;});}
  public void ContextDelete(){string id=contextId;CloseHierarchyMenu();Change(d=>d.elements.Remove(d.Find(id)));}
  public void ContextDuplicate(){string id=contextId;CloseHierarchyMenu();Change(d=>{var e=d.Find(id);if(e==null)return;var copy=JsonUtility.FromJson<SiteElement>(JsonUtility.ToJson(e));copy.id="el_"+Guid.NewGuid().ToString("N").Substring(0,12);copy.name=e.name.Length>70?e.name.Substring(0,70)+" copy":e.name+" copy";copy.x+=12;copy.y+=12;d.elements.Insert(d.elements.IndexOf(e)+1,copy);});}
  void Layer(int delta){string id=contextId;CloseHierarchyMenu();Change(d=>SitePages.MoveLayer(d,id,delta));}
  public void LayerFront(){Layer(256);}public void LayerForward(){Layer(1);}public void LayerBack(){Layer(-256);}public void LayerBackward(){Layer(-1);}
  void LayoutActions()
  {
   if(actionEditor==null||actionTail==null)return;
   var section=Array.Find(sections,s=>s.capability=="actions");if(section==null)return;
   if(tailPositions==null){tailPositions=new Vector2[actionTail.Length];for(int i=0;i<actionTail.Length;i++)tailPositions[i]=actionTail[i].anchoredPosition;actionOriginalHeight=((RectTransform)section.panel.transform).sizeDelta.y;}
   float h=Mathf.Max(84,SiteCommands.All[commandDropdown.value].Arguments.Length*102),delta=306-h;
   actionEditor.sizeDelta=new Vector2(actionEditor.sizeDelta.x,h);
   for(int i=0;i<actionEditor.childCount;i++){var child=actionEditor.GetChild(i) as RectTransform;if(child!=null)child.sizeDelta=new Vector2(child.sizeDelta.x,h);}
   for(int i=0;i<actionTail.Length;i++)actionTail[i].anchoredPosition=tailPositions[i]+new Vector2(0,delta);
   ((RectTransform)section.panel.transform).sizeDelta=new Vector2(0,actionOriginalHeight-delta);
   if(argumentHint!=null)argumentHint.gameObject.SetActive(false);
  }
  void RefreshBackground()
  {
   if(backgroundDropdown==null)return;backgroundValues=new List<string>{""};var labels=new List<string>{"No background image"};
   foreach(var a in EditingDocument.assets)if(a.media.StartsWith("image/",StringComparison.Ordinal)){backgroundValues.Add(a.name);labels.Add(a.name);}
   backgroundDropdown.ClearOptions();backgroundDropdown.AddOptions(labels);backgroundDropdown.SetValueWithoutNotify(Mathf.Max(0,backgroundValues.IndexOf(EditingDocument.backgroundImage)));
  }
  public void BackgroundChanged(int index){if(index>=0&&index<backgroundValues.Count)Change(d=>d.backgroundImage=backgroundValues[index]);}
  public void ImportBackground(){ImportImage("@background",false);}
  public void FitBrowserPage(){Change(d=>{d.viewportMode=d.viewportMode=="viewport"?"":"viewport";});Report(EditingDocument.viewportMode=="viewport"?"Размер страницы = окно браузера":"Фиксированный размер страницы");}
 }
}
