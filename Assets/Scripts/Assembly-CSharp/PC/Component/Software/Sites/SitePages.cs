using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
namespace PC.Component.Software.Sites
{
 [Serializable] public sealed class SitePage
 {
  public string path="/about",title="About",background="#FFFFFF",backgroundImage="",viewportMode="viewport";
  public float width=960,height=600;public List<SiteElement> elements=new List<SiteElement>();
 }
 public static class SitePages
 {
  public static bool SafeRoute(string path){return path!=null&&path.Length<=96&&Regex.IsMatch(path,@"^/(?:[a-z0-9_-]+(?:/[a-z0-9_-]+)*)?\z");}
  public static SiteDocument New(float width=960,float height=600){return new SiteDocument{width=Math.Max(128,Math.Min(4096,width)),height=Math.Max(128,Math.Min(16384,height)),viewportMode="viewport"};}
  public static SiteDocument Document(SiteDocument root,SitePage p)
  {
   return new SiteDocument{title=p.title,description=root.description,keywords=root.keywords,icon=root.icon,assets=root.assets,elements=p.elements,width=p.width,height=p.height,background=p.background,backgroundImage=p.backgroundImage,viewportMode=p.viewportMode};
  }
  public static SiteDocument At(SiteDocument root,string path)
  {
   if(path=="/")return root;var p=root.pages.Find(page=>page.path==path);if(p==null)throw new ArgumentException("Page not found: "+path);return Document(root,p);
  }
  public static void Apply(SiteDocument root,string path,SiteDocument doc)
  {
   if(path=="/")return;var p=root.pages.Find(page=>page.path==path);if(p==null)throw new ArgumentException("Page not found");
   p.title=doc.title;p.width=doc.width;p.height=doc.height;p.background=doc.background;p.backgroundImage=doc.backgroundImage;p.viewportMode=doc.viewportMode;p.elements=doc.elements;
   root.assets=doc.assets;root.icon=doc.icon;root.description=doc.description;root.keywords=doc.keywords;
  }
  public static void MoveLayer(SiteDocument doc,string id,int delta)
  {
   int index=doc.elements.FindIndex(e=>e.id==id);if(index<0)return;int next=Math.Max(0,Math.Min(doc.elements.Count-1,index+delta));
   var el=doc.elements[index];doc.elements.RemoveAt(index);doc.elements.Insert(next,el);
  }
 }
}
