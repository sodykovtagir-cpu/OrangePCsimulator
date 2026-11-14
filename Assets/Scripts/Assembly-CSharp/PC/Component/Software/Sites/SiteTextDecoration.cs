using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software.Sites
{
    /// <summary>Underline/strike are mesh rules, not user-interpreted rich text.
    /// Text stays literal; Bold/Italic use UGUI's native FontStyle.</summary>
    public sealed class SiteTextDecoration : BaseMeshEffect
    {
        public bool underline, strike;
        protected override void OnEnable()
        {
            base.OnEnable();var text=GetComponent<Text>();
            if(text!=null&&text.font!=null)text.font.RequestCharactersInTexture("_",text.fontSize,text.fontStyle);
        }
        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive() || (!underline && !strike)) return;
            var text = graphic as Text; if (text == null) return;
            CharacterInfo glyph;
            if (!text.font.GetCharacterInfo('_', out glyph, text.fontSize, text.fontStyle)) return;
            Vector2 solid = (glyph.uvBottomLeft + glyph.uvTopRight) * .5f;
            var generator = text.cachedTextGenerator;
            var chars = generator.characters; var lines = generator.lines;
            float unit = 1f / text.pixelsPerUnit;
            for (int line = 0; line < lines.Count; line++)
            {
                int first = lines[line].startCharIdx, last = line + 1 < lines.Count ? lines[line + 1].startCharIdx : chars.Count;
                if (first >= chars.Count || last <= first) continue;
                last = Mathf.Min(last, chars.Count);
                float left=chars[first].cursorPos.x*unit, right=left;
                for(int i=first;i<last;i++) right=Mathf.Max(right,(chars[i].cursorPos.x+chars[i].charWidth)*unit);
                float top=lines[line].topY*unit;
                if(underline) Rule(mesh,left,right,top-text.fontSize*.96f,Mathf.Max(1,text.fontSize/16f),text.color,solid);
                if(strike) Rule(mesh,left,right,top-text.fontSize*.52f,Mathf.Max(1,text.fontSize/16f),text.color,solid);
            }
        }
        static void Rule(VertexHelper mesh,float left,float right,float y,float height,Color color,Vector2 solid)
        {
            if(right<=left)return;
            int start=mesh.currentVertCount;var v=UIVertex.simpleVert;v.color=color;v.uv0=solid;
            v.position=new Vector3(left,y,0);mesh.AddVert(v);v.position=new Vector3(left,y+height,0);mesh.AddVert(v);
            v.position=new Vector3(right,y+height,0);mesh.AddVert(v);v.position=new Vector3(right,y,0);mesh.AddVert(v);
            mesh.AddTriangle(start,start+1,start+2);mesh.AddTriangle(start,start+2,start+3);
        }
    }
}
