using PC.Shop;
using UnityEngine;

public class Darknet : MonoBehaviour
{
	  public void Purchase(ShopItem item)
    {
		    Main.Instance.Buy(item);
    }
}
