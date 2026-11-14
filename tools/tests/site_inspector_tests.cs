using System;using System.Collections.Generic;using System.Text;using PC.Component.Software.Sites;
class Test {
 static int checks;static void Check(bool b){checks++;if(!b)throw new Exception("inspector rule "+checks);}static void Near(float a,float b){Check(Math.Abs(a-b)<.01f);}static void Reject(Action a){checks++;try{a();}catch(ArgumentException){return;}throw new Exception("Expected rejection "+checks);}
 static void Main(){
 var doc=new SiteDocument();var text=new SiteElement{id="text",kind="text",name="Title"};var image=new SiteElement{id="image",kind="image",name="Background",image="galaxy.jpg"};var button=new SiteElement{id="button",kind="button",name="Download"};doc.elements.AddRange(new[]{text,image,button});
 Check(!SiteEditing.Supports(image,"text")&&SiteEditing.Supports(image,"fill")&&SiteEditing.Supports(image,"imageAction"));Check(SiteEditing.Supports(text,"text")&&!SiteEditing.Supports(text,"fill")&&!SiteEditing.Supports(text,"actions"));Check(SiteEditing.Supports(button,"text")&&SiteEditing.Supports(button,"fill")&&SiteEditing.Supports(button,"actions"));
 var empty=SiteEditing.ImportFile(doc,"Tools/Device Manager.exe","");Check(empty.name=="Device Manager.exe"&&empty.Bytes().Length==0);doc.assets.Add(empty);
 var program=SiteEditing.ImportFile(doc,"Programs/My Program.exe","lua-app-package UTF8: Привет");doc.assets.Add(program);Check(Encoding.UTF8.GetString(program.Bytes()).EndsWith("Привет"));
 var duplicate=SiteEditing.ImportFile(doc,"My Program.exe","new revision");Check(duplicate.name=="My Program (2).exe");doc.assets.Add(duplicate);Check(SiteEditing.ImportFile(doc,"My Program.exe","x").name=="My Program (3).exe");
 var unicode=SiteEditing.ImportFile(doc,"Программы/Моя программа.exe","content");Check(unicode.name=="Моя программа.exe");doc.assets.Add(unicode);
 Reject(()=>SiteEditing.ImportFile(doc,"a:b.exe","x"));Reject(()=>SiteEditing.ImportFile(doc,"..","x"));Reject(()=>SiteEditing.ImportFile(doc,"x.exe",new string('x',SiteDocument.MaxAssetBytes+1)));
 Reject(()=>new SiteAsset{name="a.png",media="image/png",data=""}.Bytes());
 image.image="";SiteEditing.MakeClickable(image);Check(image.kind=="button"&&image.text==""&&image.name=="Background");image.kind="image";image.image="galaxy.jpg";float oldX=image.x,oldY=image.y,oldW=image.width;SiteEditing.MakeClickable(image);Check(image.image=="galaxy.jpg"&&image.x==oldX&&image.y==oldY&&image.width==oldW);image.kind="image";image.image="";
 var choices=SiteEditing.Choices(doc,SiteCommands.Find("SetText"),0);Check(choices.Contains("button")&&choices.Contains("text")&&!choices.Contains("image"));choices=SiteEditing.Choices(doc,SiteCommands.Find("SetImage"),0);Check(choices.Contains("image")&&!choices.Contains("text"));Check(SiteEditing.Choices(doc,SiteCommands.Find("SetEnabled"),0).Count==1);
 Check(SiteEditing.Choices(doc,SiteCommands.Find("StartDownload"),0).Contains("Device Manager.exe"));Check(SiteEditing.Choices(doc,SiteCommands.Find("SetImage"),1).Count==0);Check(SiteEditing.Choices(doc,SiteCommands.Find("SetVisible"),1).Count==2);Check(SiteEditing.Choices(doc,SiteCommands.Find("SetFont"),1).Count==3);
 foreach(var spec in SiteCommands.All)for(int i=0;i<spec.Arguments.Length;i++){string name=spec.Arguments[i];Check(SiteEditing.UsesChoice(spec,i)==(name=="id"||name=="file"||name=="font"||spec.Types[i]=="bool"));}
 foreach(int preset in new[]{1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16}){
  var d=new SiteDocument();var e=new SiteElement{x=123,y=87,width=240,height=130,pivotX=.3f,pivotY=.7f};float left=e.x-e.width*e.pivotX,top=e.y-e.height*e.pivotY;SiteEditing.SetLayout(d,e,preset);Near(e.x-e.width*e.pivotX,left);Near(e.y-e.height*e.pivotY,top);
  d.elements.Add(e);d.Validate();float l,t,r,b;SiteEditing.Anchors(preset,out l,out t,out r,out b);d.width+=200;d.height+=100;float x,y,w,h;SiteEditing.Resolve(d,e,out x,out y,out w,out h);Near(w,240+(r-l)*200);Near(h,130+(b-t)*100);Near(x,e.x+(l+(r-l)*e.pivotX)*200);Near(y,e.y+(t+(b-t)*e.pivotY)*100);
  SiteEditing.SetLayout(d,e,0);Near(e.x,x);Near(e.y,y);Near(e.width,w);Near(e.height,h);d.Validate();
 }
 Reject(()=>SiteEditing.SetLayout(doc,text,17));
 text.bold=true;text.italic=true;text.underline=true;text.strike=true;button.actions.Add(new SiteAction{command="StartDownload",arguments=new List<string>{"My Program.exe"}});
 doc.Validate();var restored=SiteDocument.Parse(doc.ToJson());Check(restored.Find("text").bold&&restored.Find("text").italic&&restored.Find("text").underline&&restored.Find("text").strike);Check(restored.Asset("Device Manager.exe").Bytes().Length==0);Check(restored.Find("button").actions[0].arguments[0]=="My Program.exe");
 var history=new SiteHistory(doc);history.Change(d=>SiteEditing.MakeClickable(d.Find("image")));Check(history.Document.Find("image").kind=="button");history.Undo();Check(history.Document.Find("image").kind=="image");history.Redo();Check(history.Document.Find("image").kind=="button");
 string url=SiteLocalAddress.Build(1,"Sites/Test.osite","Моя программа.exe");int disk;string path,asset;Check(SiteLocalAddress.TryParse(url,out disk,out path,out asset)&&asset=="Моя программа.exe");
 Console.WriteLine("PASS: "+checks+" actual import/inspector rules: spaces/Unicode/empty EXE/duplicates, capability filtering, image conversion+undo, typed choices, 16 pivot-anchor presets without jumps, stretch geometry, text-style serialization");
 }
}
