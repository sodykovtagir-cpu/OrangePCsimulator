using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MonitorRaycaster : GraphicRaycaster
{
	private Vector2 size;

	private Vector2 current;

	private bool inRange;

	private CanvasGroup group;

	protected override void Start()
	{
		var rect = GetComponent<RectTransform>();
		if (rect != null)
		{
			var r = rect.rect;
			size.x = r.width;
			size.y = r.height;
		}

		var go = gameObject;
		if (go != null) group = go.AddComponent<CanvasGroup>();

		base.Start();
	}

	public void ChangeCoordinate(Vector2 coordinate)
	{
		current = coordinate;
		inRange = true;
	}

	public override void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList)
    {
        if (eventData == null) return;

        if (inRange)
        {
            var pos = new Vector2(current.x * size.x, current.y * size.y);
            eventData.position = pos;
            base.Raycast(eventData, resultAppendList);
        }

        inRange = false;
    }
}
