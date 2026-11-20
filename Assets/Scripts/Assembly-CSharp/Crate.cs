using UnityEngine;

public class Crate : MonoBehaviour
{
	[SerializeField]
	private GameObject broken;

	/// <summary>
	/// Разбить ящик без молотка. Нужно доставке кастомной вещи: рисунок
	/// наносится на предмет уже внутри ящика, поэтому «сломать» его надо
	/// программно в момент доставки, иначе игрок открыл бы ящик и увидел
	/// крышку без рисунка.
	/// </summary>
	public void BreakApart()
	{
		Open();
	}

	/// <summary>Ящик можно открыть (в нём есть что выкладывать).</summary>
	public bool CanOpen()
	{
		return broken != null;
	}

	private void Open()
	{
		if (broken == null)
		{
			Debug.LogWarning("[Crate] Не назначен BrokenCrate — ящик не открывается.");
			return;
		}
		// Сборка «сломанного» ящика выкладывает предмет своим Box.
		broken.SetActive(true);
		broken.transform.SetParent(null);
		Destroy(gameObject);
	}

	private void OnCollisionEnter(Collision collision)
	{
		if (collision == null) return;
		if (collision.transform == null) return;
		if (!collision.transform.CompareTag("Hammer")) return;
		Open();
	}

	public void ShowTip()
	{
		Main.Instance.FadeText(Localization.GetText("Use Hammer to Open"));
	}
}
