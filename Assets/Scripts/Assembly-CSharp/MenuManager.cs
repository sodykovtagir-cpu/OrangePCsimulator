using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MenuManager : MonoBehaviour
{
	[SerializeField]
	private GameObject[] menus;

	[SerializeField]
	private AudioClip clickSound;

	[SerializeField]
	private bool addSoundToButtons;

	private AudioSource source;
	private Stack<GameObject> menuStack;

	private void Start()
	{
		source = GetComponent<AudioSource>();
		menuStack = new Stack<GameObject>();

		foreach (var menu in menus)
		{
			if (menu.activeSelf)
			{
				menuStack.Push(menu);
				break;
			}
		}
	}

	public void ShowMenu(string pageName)
	{
		if (addSoundToButtons)
		{
			PlayClickSound();
		}

		GameObject menuToShow = null;
		foreach (var menu in menus)
		{
			if (menu.name == pageName)
			{
				menuToShow = menu;
				break;
			}
		}

		if (menuToShow == null)
		{
			Debug.LogWarning($"Menu '{pageName}' not found.");
			return;
		}

		if (menuStack.Count > 0)
		{
			GameObject current = menuStack.Peek();
			if (current != menuToShow)
			{
				current.SetActive(false);
				menuStack.Push(menuToShow);
				menuToShow.SetActive(true);
			}
		}
		else
		{
			menuStack.Push(menuToShow);
			menuToShow.SetActive(true);
		}
	}

	public void Back()
	{
		if (addSoundToButtons)
		{
			PlayClickSound();
		}

		if (menuStack.Count <= 1)
			return;

		GameObject current = menuStack.Pop();
		current.SetActive(false);

		GameObject previous = menuStack.Peek();
		previous.SetActive(true);
	}

	public void HideMenu(bool hide)
	{
		if (menuStack.Count == 0)
			return;

		GameObject top = menuStack.Peek();
		top.SetActive(!hide);
	}

	public void PlayClickSound()
	{
		if (clickSound != null && source != null)
		{
			source.PlayOneShot(clickSound);
		}
	}
}
