using UnityEngine;
using UnityEngine.UI;

public class OggleResultItem : MonoBehaviour
{
    public Text title;
    public Text description;
    public RawImage icon;
    public void SetIcon(Sprite sprite)
    {
        if(title!=null)title.supportRichText=false;if(description!=null)description.supportRichText=false;
        if(icon==null)icon=GetComponentInChildren<RawImage>(true);
        if(icon==null)return;icon.texture=sprite!=null?sprite.texture:null;icon.enabled=sprite!=null;
        if(sprite!=null){var r=sprite.textureRect;icon.uvRect=new Rect(r.x/sprite.texture.width,r.y/sprite.texture.height,r.width/sprite.texture.width,r.height/sprite.texture.height);}
    }
}