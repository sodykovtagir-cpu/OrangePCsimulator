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

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        menuStack = new Stack<GameObject>();
    }

    private void Start()
    {
        foreach (var menu in menus)
        {
            if (menu == null)
                continue;

            if (menu.activeSelf)
            {
                menuStack.Push(menu);
                break;
            }
        }
    }

    public void ShowMenu(string pageName)
    {
        if (string.IsNullOrEmpty(pageName))
            return;

        if (menuStack == null)
            menuStack = new Stack<GameObject>();

        if (addSoundToButtons)
            PlayClickSound();

        GameObject menuToShow = null;

        foreach (var menu in menus)
        {
            if (menu == null)
                continue;

            if (menu.name == pageName)
            {
                menuToShow = menu;
                break;
            }
        }

        if (menuToShow == null)
        {
            Debug.LogWarning("Menu not found: " + pageName);
            return;
        }

        if (menuStack.Count > 0)
        {
            GameObject current = menuStack.Peek();

            if (current != null && current != menuToShow)
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
        if (menuStack == null || menuStack.Count <= 1)
            return;

        if (addSoundToButtons)
            PlayClickSound();

        GameObject current = menuStack.Pop();

        if (current != null)
            current.SetActive(false);

        GameObject previous = menuStack.Peek();

        if (previous != null)
            previous.SetActive(true);
    }

    public void HideMenu(bool hide)
    {
        if (menuStack == null || menuStack.Count == 0)
            return;

        GameObject top = menuStack.Peek();

        if (top != null)
            top.SetActive(!hide);
    }

    public void PlayClickSound()
    {
        if (source != null && clickSound != null)
            source.PlayOneShot(clickSound);
    }
}