using System;using System.Collections.Generic;using PC.Component.Software.Sites;using PC.Component.Software.Lua;
class Test {
 static int checks;static void Check(bool b,string message="check"){checks++;if(!b)throw new Exception(message+" "+checks);}
 static void Reject(Action action){checks++;try{action();}catch(ArgumentException){return;}catch(PcosLuaException){return;}throw new Exception("should reject "+checks);}
 static SiteDocument Doc(){var d=new SiteDocument();d.elements.Add(new SiteElement{id="title",text="Hello"});d.elements.Add(new SiteElement{id="button",kind="button",text="Go"});return d;}
 static void Main(){
 var d=Doc();var png=new SiteAsset{name="picture.png",media="image/png",data="iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jQ9sAAAAASUVORK5CYII="};d.assets.Add(png);d.icon=png.name;d.elements.Add(new SiteElement{id="image",kind="image",image=png.name});
 string json=d.ToJson();var restored=SiteDocument.Parse(json);Check(restored.elements.Count==3&&restored.assets.Count==1&&restored.icon==png.name);Check(restored.assets[0].data==png.data);
 Reject(()=>SiteDocument.Parse("{}"));Reject(()=>SiteDocument.Parse(new string('[',33)+new string(']',33)));Reject(()=>SiteDocument.Parse(""));Reject(()=>SiteDocument.Parse("{not-json"));Reject(()=>SiteDocument.Parse(new string('x',SiteDocument.MaxJsonBytes+1)));
 restored.version=2;Reject(()=>restored.Validate());restored.version=1;restored.elements[0].x=float.NaN;Reject(()=>restored.Validate());restored.elements[0].x=0;
 restored.elements[0].pivotY=2;Reject(()=>restored.Validate());restored.elements[0].pivotY=0;restored.elements[0].fill="url(http://evil)";Reject(()=>restored.Validate());restored.elements[0].fill="#FFFFFF";
 restored.elements.Add(restored.elements[0]);Reject(()=>restored.Validate());restored.elements.RemoveAt(restored.elements.Count-1);
 restored.assets.Add(new SiteAsset{name="PICTURE.PNG",media=png.media,data=png.data});Reject(()=>restored.Validate());restored.assets.RemoveAt(1);
 foreach(string path in new[]{"../secret","/root.png","a/b.png","a\\b.png","..","https://bad","name:bad"})Check(!SiteDocument.SafeName(path));
 Check(SiteDocument.SafeName("myapp.exe"));Reject(()=>new SiteAsset{name="a",media="image/png",data="bad!"}.Bytes());Reject(()=>SiteDocument.ValidateImage(png.Bytes(),"image/jpeg"));
 byte[] bomb=png.Bytes();bomb[16]=127;Reject(()=>SiteDocument.ValidateImage(bomb,"image/png"));
 foreach(string url in new[]{"file:///etc/passwd","javascript:alert(1)","http://localhost","www.good.com/../bad","www.good.com:80","www.good.com?secret=1","https://user:pass@evil.com"})Reject(()=>SiteDocument.NormalizeUrl(url));
 Check(SiteDocument.NormalizeUrl("HTTPS://WWW.ModForge.COM/")=="www.modforge.com");
 var history=new SiteHistory(d);history.Change(x=>x.title="Changed");Check(history.CanUndo&&history.Document.title=="Changed");history.Undo();Check(history.Document.title==d.title&&history.CanRedo);history.Redo();Check(history.Document.title=="Changed");
 var tooMany=Doc();while(tooMany.elements.Count<=SiteDocument.MaxElements)tooMany.elements.Add(new SiteElement{id="e"+tooMany.elements.Count});Reject(()=>tooMany.Validate());
 var imageBudget=Doc();byte[] large=png.Bytes();large[16]=0;large[17]=0;large[18]=8;large[19]=0;large[20]=0;large[21]=0;large[22]=8;large[23]=0;
 for(int i=0;i<3;i++)imageBudget.assets.Add(new SiteAsset{name="large"+i+".png",media="image/png",data=Convert.ToBase64String(large)});Reject(()=>imageBudget.Validate());
 string before=history.Document.ToJson();Reject(()=>history.Change(x=>x.width=-1));Check(history.Document.ToJson()==before);history.Undo();history.Change(x=>x.title="Branch");Check(!history.CanRedo);
 var rootFile=new SiteAsset{name="myapp.exe",media="application/x-pcos-file",data=Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("safe in-game file content"))};d.assets.Add(rootFile);
 string destination="",message="",changed="";SiteAsset downloaded=null;bool visible=false;
 var runtime=new SiteLua(d){Redirect=s=>destination=s,Download=a=>downloaded=a,Message=s=>message=s,TextChanged=(id,s)=>changed=s,VisibilityChanged=(id,b)=>visible=b};
 d.Find("button").onClick="RedirectURL(\"www.modforge.com\")";runtime.Click("button");Check(destination=="www.modforge.com");
 d.Find("button").onClick="StartDownload(\"site/myapp.exe\")";runtime.Click("button");Check(downloaded==rootFile);
 d.Find("button").onClick="SetText(\"title\", \"Updated\") Alert(GetText(\"title\")) SetVisible(\"title\", true)";runtime.Click("button");Check(changed=="Updated"&&message=="Updated"&&visible);
 foreach(string global in new[]{"os","fs","admin","pc","terminal","io","package","require","string","table","pcall"}){d.Find("button").onClick="Alert(type("+global+"))";runtime.Click("button");Check(message=="nil",global);}
 foreach(string code in new[]{"StartDownload(\"../secret\")","RedirectURL(\"file:///root\")","StartDownload(\"missing.exe\")","SetText(\"missing\", \"x\")","while true do end","function recurse() recurse() end recurse()","local s=\"xxxx\" for i=1,30 do s=s..s end","for i=1,40 do Alert(\"spam\") end"}){d.Find("button").onClick=code;Reject(()=>runtime.Click("button"));}
 d.Find("button").onClick="Alert(\"recovered\")";runtime.Click("button");Check(message=="recovered");
 d.Find("button").onClick=string.Concat(new string[0]);runtime.Click("button");runtime.Click("missing");
 // Preserve unrelated app API/default operation ceiling.
 var normal=new PcosLua();Check(normal.OperationLimit==PcosLua.MaxOps&&normal.StringLimit==0);Check(normal.DoString("return string.rep(\"x\", 3)").AsString()=="xxx");
 Console.WriteLine("PASS: "+checks+" actual site model/history/Lua checks: round trip, paths, bounds, images, rollback, capabilities, recursion, CPU/string/action budgets; app defaults retained");
 }
}
