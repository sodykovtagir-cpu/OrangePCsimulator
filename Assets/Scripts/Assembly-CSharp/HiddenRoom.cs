using TinyJSON;
using UnityEngine;
using UnityEngine.EventSystems;

public class HiddenRoom : MonoBehaviour, IPointerClickHandler, IEventSystemHandler
{
	[SerializeField]
	private int clickCount;
#if UNITY_STANDALONE
	private int clicks;
#endif

	public void OnPointerClick(PointerEventData eventData)
	{
#if UNITY_STANDALONE
		clicks += 1;
		if (clicks >= clickCount) {
			clicks = 0;
			LoadScene();
		}
#elif UNITY_ANDROID || UNITY_IOS
		if (eventData == null) return;
		if (eventData.pointerId == -1) return;
		if (Input.touchCount <= 0) return;

		var touches = Input.touches;
		for (int i = 0; i < touches.Length; i++)
		{
			var t = touches[i];
			if (t.fingerId == eventData.pointerId)
			{
				if (t.tapCount >= clickCount) LoadScene();
				break;
			}
		}
#endif
	}

	private void LoadScene()
	{
		MainMenu.Instance.LoadExample("Hidden Room");
		CloudOnceManager.Instance.GetAchievementFromId("the_hidden_room")?.Unlock(null);
	}
}
