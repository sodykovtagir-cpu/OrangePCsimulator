using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace PC.Component.Software.Sites
{
 public sealed class SiteHierarchyContext:MonoBehaviour,IPointerClickHandler,IPointerDownHandler,IPointerUpHandler,IBeginDragHandler,IDragHandler,IEndDragHandler
 {
  ScrollRect scroll;SiteEditor editor;string id;bool held;float at;Vector2 point;Camera camera;
  public void Bind(SiteEditor owner,string element){editor=owner;id=element;scroll=GetComponentInParent<ScrollRect>();}
  public void OnPointerClick(PointerEventData e){if(e.button==PointerEventData.InputButton.Right)editor.OpenHierarchyMenu(id,e.position,e.pressEventCamera);}
  public void OnPointerDown(PointerEventData e){held=e.button==PointerEventData.InputButton.Left;at=Time.unscaledTime;point=e.position;camera=e.pressEventCamera;}
  public void OnPointerUp(PointerEventData e){held=false;}
  public void OnBeginDrag(PointerEventData e){held=false;if(scroll!=null)scroll.OnBeginDrag(e);}
  public void OnDrag(PointerEventData e){held=false;if(scroll!=null)scroll.OnDrag(e);}
  public void OnEndDrag(PointerEventData e){held=false;if(scroll!=null)scroll.OnEndDrag(e);}
  void Update(){if(held&&Time.unscaledTime-at>=.55f){held=false;editor.OpenHierarchyMenu(id,point,camera);}}
  void OnDisable(){held=false;}
 }
}
