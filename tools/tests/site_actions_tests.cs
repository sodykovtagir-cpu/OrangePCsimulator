using System;using System.Collections.Generic;using System.Text;using PC.Component.Software.Sites;
class Test {
 static int checks;static void Check(bool b,string m="check"){checks++;if(!b)throw new Exception(m+" "+checks);}static void Reject(Action a){checks++;try{a();}catch(ArgumentException){return;}throw new Exception("must reject "+checks);}
 static SiteAction Action(string command,params string[] args){return new SiteAction{command=command,arguments=new List<string>(args)};}
 static void Main(){
 var doc=new SiteDocument();var label=new SiteElement{id="label",text="Hello"};var button=new SiteElement{id="trigger",kind="button"};doc.elements.Add(label);doc.elements.Add(button);
 var note=new SiteAsset{name="test.txt",media="application/x-pcos-file",data=Convert.ToBase64String(Encoding.UTF8.GetBytes("Site root text"))};doc.assets.Add(note);
 var image=new SiteAsset{name="pixel.png",media="image/png",data="iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jQ9sAAAAASUVORK5CYII="};doc.assets.Add(image);
 string redirect="",message="";SiteAsset opened=null,downloaded=null;int style=0;var runtime=new SiteLua(doc){URL="file://disk/12/MySite.osite",OpenFile=a=>opened=a,Download=a=>downloaded=a,Redirect=s=>redirect=s,Message=s=>message=s,ElementChanged=id=>style++};
 foreach(var command in SiteCommands.All){
  var args=new List<string>();foreach(string name in command.Arguments){switch(name){
   case "id":args.Add("label");break;case "file":args.Add(command.Name=="SetImage"?"pixel.png":"test.txt");break;
   case "page":args.Add("/about");break;case "url":args.Add("www.modforge.com");break;case "color":args.Add("#AABBCC");break;case "font":args.Add("mono");break;
   case "text":args.Add("Updated");break;case "visible":case "enabled":args.Add("true");break;
   case "x":case "y":args.Add(command.Name=="SetPivot"?"0.5":"12");break;
   case "size":args.Add("32");break;default:args.Add("80");break;}}
  button.actions=new List<SiteAction>{new SiteAction{command=command.Name,arguments=args}};runtime.Click("trigger");Check(true,command.Name);if(command.Name=="RedirectURL")Check(redirect=="www.modforge.com");
 }
 Check(opened==note&&downloaded==note);Check(redirect=="/about");Check(style>10);Check(message=="true");
 button.actions=new List<SiteAction>{Action("SetText","label","Quoted \" ) Alert(\"BAD\") -- "),Action("AppendText","label","Suffix")};runtime.Click("trigger");Check(label.text.EndsWith("Suffix")&&label.text.Contains("Alert"));
 button.actions.Clear();button.onClick=SiteCommands.Compile(Action("SetText","label","\" ); Alert(\"bad\") --\nline"));runtime.Click("trigger");Check(label.text=="\" ); Alert(\"bad\") --\nline");button.onClick="";
 var saved=SiteDocument.Parse(doc.ToJson());Check(saved.Find("trigger").actions.Count==0);
 button.actions.Add(Action("OpenFile","test.txt"));saved=SiteDocument.Parse(doc.ToJson());Check(saved.Find("trigger").actions[0].arguments[0]=="test.txt");
 Reject(()=>SiteCommands.Validate(Action("os.shutdown")));Reject(()=>SiteCommands.Validate(Action("OpenFile")));Reject(()=>SiteCommands.Validate(Action("SetVisible","label","1")));Reject(()=>SiteCommands.Validate(Action("SetSize","label","NaN","2")));
 Check(SiteCommands.ReadArgumentLines("test.txt",1)[0]=="test.txt");Check(SiteCommands.ReadArgumentLines("label\nHello",2).Count==2);Reject(()=>SiteCommands.ReadArgumentLines("unexpected",0));
 button.actions=new List<SiteAction>{Action("SetSize","label","90","-1")};float width=label.width;Reject(()=>runtime.Click("trigger"));Check(label.width==width);
 button.actions=new List<SiteAction>{Action("OpenFile","../visitor.txt")};Reject(()=>runtime.Click("trigger"));button.actions.Clear();
 button.actions=new List<SiteAction>();for(int i=0;i<32;i++)button.actions.Add(Action("Alert","ok"));button.onClick="Alert(\"one too many\")";Reject(()=>runtime.Click("trigger"));button.onClick="";runtime.Click("trigger");Check(message=="ok");
 button.actions=null;doc.Validate();Check(button.actions!=null);button.actions.Add(Action("GetTitle"));runtime.Click("trigger");Check(message==doc.title);button.actions.Clear();
 string[] bad={"file:///etc/passwd","https://example.com","file://disk/1/Sites/System/secret.osite","file://disk/1/%20System%20/secret.osite","file://disk/1/System/secret.osite","file://disk/1/%53ystem/secret.osite","file://disk/1/../secret.osite","file://disk/1/Sites%2fsecret.osite","file://disk/-1/site.osite","file://disk/1/site.osite?x=1","file://disk/1/site.osite#site/../secret","file://disk/1/site.osite#site/%2fsecret","file://disk/1/test.exe"};
 foreach(string url in bad){int disk;string path,asset;Check(!SiteLocalAddress.TryParse(url,out disk,out path,out asset),url);}
 foreach(string path in new[]{"MySite.osite","Sites/Mixed CASE/My Site.osite","Сайты/Тест.osite"}){string url=SiteLocalAddress.Build(123,path,"test.txt");int disk;string restoredPath,asset;Check(SiteLocalAddress.TryParse(url,out disk,out restoredPath,out asset));Check(disk==123&&restoredPath==path&&asset=="test.txt");}
 Console.WriteLine("PASS: "+checks+" real site action/catalog/address checks; "+SiteCommands.All.Length+" commands, structured/legacy Lua, safe arguments, round trip, bounded mutation and root-only files");
 }
}
