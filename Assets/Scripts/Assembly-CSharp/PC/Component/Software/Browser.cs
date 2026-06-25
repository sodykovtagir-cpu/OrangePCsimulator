using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
    public class Browser : App
    {
        [Serializable]
        private class WebsiteItem
        {
            public string url;
            public Website page;
            public bool visible;
        }

        [Header("UI")]
        [SerializeField] private GameObject home;
        [SerializeField] private WebsiteItem[] websites;
        [SerializeField] private InputField addressBar;
        [SerializeField] private Transform quickAccessPrefab;
        [SerializeField] private Transform quickAccessParent;
        [SerializeField] private Transform content;
        [SerializeField] private GameObject loading;

        [Header("Navigation Buttons")]
        [SerializeField] private Button backButton;
        [SerializeField] private Button forwardButton;
        [SerializeField] private Button refreshButton;

        [Header("Loading Bar")]
        [SerializeField] private GameObject loadingBar;

        private Coroutine loadingCoroutine;
        private Website web;
        private Image background;

        // ✅ Реальный WebView
        private WebViewObject realWebView;
        private bool usingRealWeb;
        private string currentUrl;

        protected override void Start()
        {
            base.Start();

            if (content != null)
                background = content.GetComponent<Image>();

#if UNITY_STANDALONE_WIN || UNITY_ANDROID

            realWebView = gameObject.AddComponent<WebViewObject>();

            realWebView.Init(
                cb: (msg) => { },
                err: (msg) => { Debug.Log("WebView Error: " + msg); },
                started: (msg) =>
                {
                    if (loadingBar != null)
                        loadingBar.SetActive(true);
                },
                hooked: (msg) => { },
                ld: (msg) =>
                {
                    if (loadingBar != null)
                        loadingBar.SetActive(false);
                }
            );

            realWebView.SetVisibility(false);

#endif

            if (backButton != null)
                backButton.onClick.AddListener(GoBack);

            if (forwardButton != null)
                forwardButton.onClick.AddListener(GoForward);

            if (refreshButton != null)
                refreshButton.onClick.AddListener(RefreshPage);

            CreateQuickAccess();
        }

        private void CreateQuickAccess()
        {
            if (websites == null) return;

            for (int i = 0; i < websites.Length; i++)
            {
                var w = websites[i];
                if (w == null || !w.visible) continue;

                var t = Instantiate(quickAccessPrefab, quickAccessParent);
                if (t == null) continue;

                var icon = t.GetChild(0)?.GetComponent<Image>();
                if (icon != null && w.page != null)
                    icon.sprite = w.page.icon;

                var txt = t.GetChild(1)?.GetComponent<Text>();
                if (txt != null && w.page != null)
                    txt.text = w.page.websiteName;

                int index = i;
                t.GetComponent<Button>()?.onClick.AddListener(() => QuickAccess(index));
            }
        }

        public void Home()
        {
            if (addressBar != null)
                addressBar.text = "";

            if (home != null)
                home.SetActive(true);

            if (background != null)
                background.enabled = true;

            if (web != null)
            {
                Destroy(web.gameObject);
                web = null;
            }

#if UNITY_STANDALONE_WIN || UNITY_ANDROID
            if (realWebView != null)
            {
                realWebView.SetVisibility(false);
                realWebView.LoadURL("about:blank");
            }
            usingRealWeb = false;
#endif
        }

        private void QuickAccess(int index)
        {
            if (websites == null || index < 0 || index >= websites.Length) return;

            addressBar.text = websites[index].url;
            Search();
        }

        public void Search()
        {
            if (loadingCoroutine != null)
                StopCoroutine(loadingCoroutine);

            loadingCoroutine = StartCoroutine(LoadingAnimation());
        }

        private IEnumerator LoadingAnimation()
        {
            if (home != null)
                home.SetActive(false);

            if (loading != null)
                loading.SetActive(true);

            if (background != null)
                background.enabled = true;

            if (web != null)
            {
                Destroy(web.gameObject);
                web = null;
            }

            string url = addressBar != null ? addressBar.text : "";

            yield return new WaitForSeconds(1f);

            bool found = false;

            if (websites != null)
            {
                foreach (var item in websites)
                {
                    if (item == null) continue;

                    if (item.url == url)
                    {
                        var instance = Instantiate(item.page, content);
                        web = instance;
                        web.Init(system);

                        if (background != null)
                            background.enabled = false;

                        found = true;
                        break;
                    }
                }
            }

            // ✅ Если сайт не внутриигровой → открываем реальный
            if (!found && !string.IsNullOrEmpty(url))
            {
                if (!url.StartsWith("http"))
                    url = "https://" + url;

#if UNITY_STANDALONE_WIN || UNITY_ANDROID

                usingRealWeb = true;
                currentUrl = url;

                if (background != null)
                    background.enabled = false;

                realWebView.SetMargins(0, 120, 0, 0);
                realWebView.SetVisibility(true);
                realWebView.LoadURL(url);

#else
                Application.OpenURL(url);
#endif
            }

            if (loading != null)
                loading.SetActive(false);
        }

        private void GoBack()
        {
#if UNITY_STANDALONE_WIN || UNITY_ANDROID
            if (usingRealWeb && realWebView != null)
                realWebView.GoBack();
#endif
        }

        private void GoForward()
        {
#if UNITY_STANDALONE_WIN || UNITY_ANDROID
            if (usingRealWeb && realWebView != null)
                realWebView.GoForward();
#endif
        }

        private void RefreshPage()
        {
#if UNITY_STANDALONE_WIN || UNITY_ANDROID
            if (usingRealWeb && realWebView != null)
                realWebView.Reload();
#endif
        }

        private void OnDestroy()
        {
#if UNITY_STANDALONE_WIN || UNITY_ANDROID
            if (realWebView != null)
                Destroy(realWebView);
#endif
        }
    }
}