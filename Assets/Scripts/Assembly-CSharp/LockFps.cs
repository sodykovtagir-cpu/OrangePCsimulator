using UnityEngine;

public class LockFps : MonoBehaviour
{
	[SerializeField]
	private int fps = 30;

	private void Update()
    {
		if (Application.targetFrameRate == fps) return;
		Application.targetFrameRate = fps;
    }
}
