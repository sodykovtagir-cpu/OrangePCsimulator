using UnityEngine;

/// <summary>
/// Рисует экран внутриигрового монитора с пониженной частотой.
/// </summary>
/// <remarks>
/// Экран ПК — это обычный Canvas, который снимает отдельная камера в
/// RenderTexture. Пока камера была включена, Unity рисовала её каждый кадр:
/// при трёх мониторах в кадре это втрое больше работы, чем на сам кадр игры,
/// потому что текстура экрана крупнее, чем занимаемое им место на экране
/// телефона.
///
/// Содержимое же меняется несравнимо реже: часы раз в секунду, окна по
/// нажатию. Пятнадцати обновлений в секунду хватает, чтобы интерфейс
/// выглядел живым, и это вчетверо меньше работы.
///
/// Компонент вешается на объект камеры в DisplayManager.CreateDisplay.
/// </remarks>
public class DisplayCameraPacer : MonoBehaviour
{
	[SerializeField]
	private Camera target;

	/// <summary>Сколько раз в секунду перерисовывать экран.</summary>
	[SerializeField]
	private float redrawsPerSecond = 15f;

	private float nextRedraw;
	private bool visible = true;

	public void Setup(Camera cam)
	{
		target = cam;
		if (target != null) target.enabled = false;
	}

	public void SetVisible(bool value)
	{
		visible = value;
	}

	private void LateUpdate()
	{
		if (target == null || !visible) return;

		// На слабых устройствах обновляем экраны ещё реже: там каждый лишний
		// проход растеризатора заметен, а рассматривать монитор в упор
		// игрок в этот момент обычно не собирается.
		float rate = redrawsPerSecond;
		if (QualitySettings.GetQualityLevel() <= 1) rate *= 0.5f;
		if (rate <= 0f) return;

		float now = Time.unscaledTime;
		if (now < nextRedraw) return;
		nextRedraw = now + 1f / rate;

		target.Render();
	}
}
