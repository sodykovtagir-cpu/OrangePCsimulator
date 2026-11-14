using System;
using System.Collections.Generic;
using PC.Component.Software.Sites;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
    public sealed partial class SiteEditor
    {
        [Serializable] public sealed class InspectorSection { public string capability; public GameObject panel; }
        [Serializable] public sealed class ArgumentControl { public GameObject root; public Text label; public Dropdown choice; public InputField input; public Button color; }
        [SerializeField] private InspectorSection[] sections;
        [SerializeField] private ArgumentControl[] argumentControls;
        [SerializeField] private Dropdown pivotDropdown, fontDropdown;
        [SerializeField] private Button boldButton, italicButton, underlineButton, strikeButton;
        [SerializeField] private RectTransform assetRoot;
        [SerializeField] private GameObject assetRowTemplate;
        [SerializeField] private ColorPicker colorPicker;
        readonly List<List<string>> argumentOptions = new List<List<string>>();
        int editEpoch;

        void InitInspector()
        {
            pivotDropdown.ClearOptions();pivotDropdown.AddOptions(new List<string>(SiteEditing.LayoutNames));
            pivotDropdown.onValueChanged.AddListener(index=>{ var id=selected; if(EditingDocument.Find(id)!=null)Change(d=>SiteEditing.SetLayout(d,d.Find(id),index)); });
            fontDropdown.ClearOptions();fontDropdown.AddOptions(new List<string>{"Default","Monospace","Medium"});
            fontDropdown.onValueChanged.AddListener(index=>SetField("font",new[]{"default","mono","medium"}[index]));
            ConfigureArguments(null);
        }
        void RefreshInspector(SiteElement element)
        {
            if(sections==null)return;
            ConfigureArguments(ReadArguments(false));LayoutActions();
            float y=44;
            foreach(var section in sections)
            {
                bool show=SiteEditing.Supports(element,section.capability);
                section.panel.SetActive(show);
                if(!show)continue;
                var rect=(RectTransform)section.panel.transform;rect.anchoredPosition=new Vector2(0,-y);y+=rect.sizeDelta.y+8;
            }
            ((RectTransform)elementPanel.transform).sizeDelta=new Vector2(0,y+6);
            if(element!=null)
            {
                pivotDropdown.SetValueWithoutNotify(element.layout);
                fontDropdown.SetValueWithoutNotify(element.font=="mono"?1:element.font=="medium"?2:0);
                Mark(boldButton,element.bold);Mark(italicButton,element.italic);Mark(underlineButton,element.underline);Mark(strikeButton,element.strike);
            }
            RefreshAssets();
            RefreshBackground();
        }
        static void Mark(Button button,bool selected)
        { if(button!=null)button.GetComponent<Image>().color=selected?new Color(1,.72f,.35f):new Color(.83f,.83f,.83f); }
        public void ToggleBold(){ToggleStyle("bold");} public void ToggleItalic(){ToggleStyle("italic");}
        public void ToggleUnderline(){ToggleStyle("underline");} public void ToggleStrike(){ToggleStyle("strike");}
        void ToggleStyle(string style)
        {
            if(!SiteEditing.Supports(EditingDocument.Find(selected),"text"))return;
            Change(d=>{var e=d.Find(selected);switch(style){case "bold":e.bold=!e.bold;break;case "italic":e.italic=!e.italic;break;case "underline":e.underline=!e.underline;break;case "strike":e.strike=!e.strike;break;}});
        }
        public void EnableImageClick()
        {
            if(EditingDocument.Find(selected)==null)return;
            Change(d=>SiteEditing.MakeClickable(d.Find(selected)));
        }
        public void PickTextColor(){PickFieldColor("textColor");} public void PickFillColor(){PickFieldColor("fill");} public void PickBackgroundColor(){PickFieldColor("background");}
        void PickFieldColor(string field)
        {
            var e=EditingDocument.Find(selected);
            if(field!="background"&&!SiteEditing.Supports(e,field=="fill"?"fill":"text"))return;
            string id=selected;int epoch=editEpoch;
            string value=field=="background"?EditingDocument.background:field=="fill"?e.fill:e.textColor;
            Pick(value,color=>{if(epoch!=editEpoch)return;Change(d=>{if(field=="background")d.background=color;else {var target=d.Find(id);if(target==null)return;if(field=="fill")target.fill=color;else target.textColor=color;}});});
        }
        void Pick(string initial,Action<string> completed)
        {
            if(colorPicker==null)return;
            colorPicker.transform.SetAsLastSibling();
            try { colorPicker.PickColor(SiteWidgets.ColorValue(initial),color=>completed("#"+ColorUtility.ToHtmlStringRGBA(color))); }
            catch(Exception ex) { Report(ex.Message); }
        }
        void RefreshAssets()
        {
            if(assetRoot==null||assetRowTemplate==null)return;
            ClearRows(assetRoot);int i=0;
            foreach(var asset in EditingDocument.assets)
            {
                string name=asset.name;var row=Instantiate(assetRowTemplate,assetRoot);row.SetActive(true);
                var rect=(RectTransform)row.transform;rect.anchoredPosition=new Vector2(4,-i++*36);rect.sizeDelta=new Vector2(-8,32);
                row.transform.Find("Name").GetComponent<Text>().text=name+" ("+asset.Bytes().Length+" B)";
                var icon=row.transform.Find("Icon").GetComponent<Image>();icon.sprite=system!=null?system.GetFileTypeSprite(name):null;icon.preserveAspect=true;
                row.transform.Find("Delete").GetComponent<Button>().onClick.AddListener(()=>RemoveAsset(name));
            }
            assetRoot.sizeDelta=new Vector2(0,Mathf.Max(36,i*36));
        }
        void RemoveAsset(string name)
        {
            try { project.Change(root=>
            {
                if(root.icon==name)root.icon="";
                var docs=new List<SiteDocument>{root};foreach(var page in root.pages)docs.Add(SitePages.Document(root,page));
                foreach(var doc in docs){if(doc.backgroundImage==name)doc.backgroundImage="";foreach(var e in doc.elements)if(e.image==name)e.image="";}
                foreach(var page in root.pages)if(page.backgroundImage==name)page.backgroundImage="";
                root.assets.Remove(root.Asset(name));
            });Redraw(); }catch(Exception ex){Report(ex.Message);return;}
            Report("Удалён файл: "+name+". Проверь действия, которые ссылались на него.");
        }
        void CommandChanged()
        { selectedAction=-1;ConfigureArguments(null);BuildInspector(); }
        void ConfigureArguments(List<string> values)
        {
            if(argumentControls==null)return;
            var spec=SiteCommands.All[commandDropdown.value];argumentOptions.Clear();
            for(int i=0;i<argumentControls.Length;i++)
            {
                var row=argumentControls[i];bool show=i<spec.Arguments.Length;row.root.SetActive(show);
                argumentOptions.Add(new List<string>());if(!show)continue;
                string value=values!=null&&i<values.Count?values[i]:"";row.label.text=spec.Arguments[i]+" ("+spec.Types[i]+")";
                bool choice=SiteEditing.UsesChoice(spec,i);row.choice.gameObject.SetActive(choice);row.input.gameObject.SetActive(!choice);
                row.color.gameObject.SetActive(!choice&&spec.Arguments[i]=="color");
                if(choice)
                {
                    var options=SiteEditing.Choices(EditingDocument,spec,i);var titles=new List<string>{"— Choose —"};
                    var stored=new List<string>{""};
                    foreach(string option in options)
                    {
                        stored.Add(option);var element=spec.Arguments[i]=="id"?EditingDocument.Find(option):null;
                        titles.Add(element==null?option:element.name+" ["+element.kind+"] · "+option);
                    }
                    int index=stored.IndexOf(value);
                    if(index<0){index=stored.Count;stored.Add(value);titles.Add("Missing / unsupported: "+value);}
                    argumentOptions[i]=stored;row.choice.ClearOptions();row.choice.AddOptions(titles);row.choice.SetValueWithoutNotify(index);
                }
                else row.input.SetTextWithoutNotify(value);
            }
            UpdateArgumentHint();
        }
        List<string> ReadArguments(bool validate)
        {
            var values=new List<string>();if(argumentControls==null)return values;
            var spec=SiteCommands.All[commandDropdown.value];
            for(int i=0;i<spec.Arguments.Length;i++)
            {
                var row=argumentControls[i];bool choice=SiteEditing.UsesChoice(spec,i);
                string value=choice?(argumentOptions.Count>i&&row.choice.value<argumentOptions[i].Count?argumentOptions[i][row.choice.value]:""):row.input.text;
                if(validate&&choice&&!SiteEditing.Choices(EditingDocument,spec,i).Contains(value))throw new ArgumentException("Выбери допустимый "+spec.Arguments[i]+" из списка");
                values.Add(value);
            }
            return values;
        }
        public void PickArgument0(){PickArgument(0);}public void PickArgument1(){PickArgument(1);}public void PickArgument2(){PickArgument(2);}
        void PickArgument(int index)
        {
            int command=commandDropdown.value,epoch=editEpoch;var row=argumentControls[index];
            Pick(string.IsNullOrEmpty(row.input.text)?"#FFFFFF":row.input.text,color=>{if(epoch==editEpoch&&command==commandDropdown.value)row.input.SetTextWithoutNotify(color);});
        }
    }
}
