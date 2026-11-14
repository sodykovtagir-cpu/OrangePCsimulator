using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PC.Component.Software.Sites
{
    /// <summary>Pure editor rules shared by the inspector and regression tests.</summary>
    public static class SiteEditing
    {
        public static readonly string[] LayoutNames = { "Custom pivot", "Top left", "Top center", "Top right", "Middle left", "Center", "Middle right", "Bottom left", "Bottom center", "Bottom right", "Stretch horizontal / top", "Stretch horizontal / middle", "Stretch horizontal / bottom", "Stretch vertical / left", "Stretch vertical / center", "Stretch vertical / right", "Stretch all" };
        // Anchor coordinates use the same top-left origin as site geometry.
        public static void Anchors(int preset, out float left, out float top, out float right, out float bottom)
        {
            left = top = right = bottom = 0;
            if (preset >= 1 && preset <= 9) { left = right = ((preset - 1) % 3) * .5f; top = bottom = ((preset - 1) / 3) * .5f; }
            else if (preset >= 10 && preset <= 12) { right = 1; top = bottom = (preset - 10) * .5f; }
            else if (preset >= 13 && preset <= 15) { bottom = 1; left = right = (preset - 13) * .5f; }
            else if (preset == 16) { right = bottom = 1; }
        }
        public static void SetLayout(SiteDocument doc, SiteElement e, int preset)
        {
            if (preset < 0 || preset >= LayoutNames.Length) throw new ArgumentException("Unknown pivot/anchor preset");
            // Resolve old anchors before changing the reference. No visual jump.
            float x, y, w, h; Resolve(doc, e, out x, out y, out w, out h);
            e.x = x; e.y = y; e.width = w; e.height = h;
            e.layout = preset; e.layoutWidth = doc.width; e.layoutHeight = doc.height;
            if (preset > 0)
            {
                float l,t,r,b; Anchors(preset,out l,out t,out r,out b);
                float px = (l+r)*.5f, py = (t+b)*.5f;
                e.x += (px-e.pivotX)*e.width; e.y += (py-e.pivotY)*e.height;
                e.pivotX=px; e.pivotY=py;
            }
        }
        public static void Resolve(SiteDocument doc, SiteElement e, out float x, out float y, out float w, out float h)
        {
            float l,t,r,b; Anchors(e.layout,out l,out t,out r,out b);
            float dw=e.layout==0?0:doc.width-e.layoutWidth, dh=e.layout==0?0:doc.height-e.layoutHeight;
            x=e.x+dw*(l+(r-l)*e.pivotX);y=e.y+dh*(t+(b-t)*e.pivotY);
            w=e.width+dw*(r-l);h=e.height+dh*(b-t);
        }
        public static bool Supports(SiteElement e, string capability)
        {
            if(e==null)return false;
            return capability=="all" || capability=="text" && e.kind!="image" || capability=="fill" && e.kind!="text" || capability=="actions" && e.kind=="button" || capability=="imageAction" && e.kind=="image" || capability=="custom" && e.layout==0;
        }
        public static void MakeClickable(SiteElement e)
        {
            if(e==null || e.kind!="image")throw new ArgumentException("Select an Image");
            e.kind="button";e.text=""; // retain the imported fill, geometry and name
        }
        public static SiteAsset ImportFile(SiteDocument doc, string path, string content)
        {
            string name=(path??"").Replace('\\','/');name=name.Substring(name.LastIndexOf('/')+1);
            if(!SiteDocument.SafeName(name))throw new ArgumentException("Недопустимое имя файла: "+name);
            string ext=Path.GetExtension(name), stem=Path.GetFileNameWithoutExtension(name), candidate=name;
            for(int i=2;doc.Asset(candidate)!=null;i++)
            {
                string suffix=" ("+i+")"+ext; candidate=stem.Substring(0,Math.Min(stem.Length,80-suffix.Length))+suffix;
            }
            var asset=new SiteAsset{name=candidate,media="application/x-pcos-file",data=Convert.ToBase64String(Encoding.UTF8.GetBytes(content??""))};
            asset.Bytes();return asset;
        }
        public static List<string> Choices(SiteDocument doc, SiteCommand spec, int argument)
        {
            string name=spec.Arguments[argument];var values=new List<string>();
            if(name=="id")
            {
                foreach(var e in doc.elements)
                {
                    bool text=spec.Name=="SetText"||spec.Name=="AppendText"||spec.Name=="ClearText"||spec.Name=="GetText"||spec.Name=="SetFont"||spec.Name=="SetFontSize"||spec.Name=="SetTextColor";
                    bool fill=spec.Name=="SetFillColor"||spec.Name=="SetImage"||spec.Name=="ClearImage";
                    if(text&&e.kind=="image"||fill&&e.kind=="text"||(spec.Name=="SetEnabled"||spec.Name=="IsEnabled")&&e.kind!="button")continue;
                    values.Add(e.id);
                }
            }
            else if(name=="file") { foreach(var a in doc.assets)if(spec.Name!="SetImage"||a.media.StartsWith("image/",StringComparison.Ordinal))values.Add(a.name); }
            else if(name=="font")values.AddRange(new[]{"default","mono","medium"});
            else if(spec.Types[argument]=="bool")values.AddRange(new[]{"true","false"});
            return values;
        }
        public static bool UsesChoice(SiteCommand spec,int argument) { string n=spec.Arguments[argument];return n=="id"||n=="file"||n=="font"||spec.Types[argument]=="bool"; }
    }
}
