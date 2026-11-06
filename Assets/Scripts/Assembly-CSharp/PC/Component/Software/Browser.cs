using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using PC.Component.Networking;

namespace PC.Component.Software
{
    public class Browser : App
    {
        [Serializable]
        public class WebsiteItem
        {
            public string url;
            public Website page;
            public bool visible;
            [Min(0f)] public float trafficMB = 5f; // fake download size for locally authored pages

            [TextArea(2, 4)]
            public string description;

            [TextArea(1, 3)]
            public string[] keywords;
        }

        [Header("UI")]
        [SerializeField] private WebsiteItem[] websites;
        [SerializeField] private InputField addressBar;

        [SerializeField] private GameObject oggleHome;
        [SerializeField] private Transform resultsContainer;
        [SerializeField] private Transform pageContainer;
        [SerializeField] private GameObject loadingPage;
        [SerializeField] private Text loadingText;
        [SerializeField] private GameObject errorPage;
        [SerializeField] private Text errorText;
        [SerializeField] private GameObject oggleResultPrefab;

        [SerializeField] private InputField homeSearchInput;

        [SerializeField] private GameObject resultsPage;
        [SerializeField] private InputField resultsSearchInput;

        public void SearchFromResults()
        {
            if (resultsSearchInput == null) return;

            addressBar.text = resultsSearchInput.text;  // синхронизируем верхнюю строку
            Search();
        }

        private Website currentPage;
        private GameObject networkPage;
        private Coroutine pendingLoad;

        protected override void Start()
        {
            base.Start();
            ShowHome();
        }

        // ================= HOME =================

        public void ShowHome()
        {
            ClearPage();
            oggleHome.SetActive(true);
        }

        // ================= SEARCH =================

        public void Search()
        {
            if (addressBar == null) return;
            string query = addressBar.text.Trim();
            if (string.IsNullOrEmpty(query)) return;
            var url = NormalizeUrl(query);
            // An authored Website prefab wins over the temporary UI when the
            // user later builds their own router/provider pages.
            if (websites != null) foreach (var site in websites)
                if (site != null && site.page != null && NormalizeUrl(site.url) == url)
                { OpenSite(site); return; }
            if (RouterWebPages.IsLocalAddress(url))
            {
                ShowNetworkPage(url);
                return;
            }
            // Searching is a real (simulated) request, even though site
            // prefabs themselves are local assets.
            LoadOnline(url, .3f, () => ShowResults(query.ToLowerInvariant()));
        }

        private static string NormalizeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return "";
            url = url.Trim().ToLowerInvariant();
            if (url.StartsWith("https://")) url = url.Substring(8);
            else if (url.StartsWith("http://")) url = url.Substring(7);
            return url.TrimEnd('/');
        }

        public void ShowNetworkPage(string url)
        {
            url = NormalizeUrl(url);
            var board = system != null ? system.Board : null;
            if (InternetService.Instance == null ||
                !InternetService.Instance.CanOpenLocalPanel(board, out var router))
            {
                ShowError("Сначала подключись к роутеру через Wi-Fi в панели задач PCOS или LAN");
                return;
            }
            ClearPage();
            oggleHome.SetActive(false);
            networkPage = RouterWebPages.Create(this, pageContainer, board, url);
        }

        private void ShowError(string message)
        {
            ClearPage();
            oggleHome.SetActive(false);
            if (errorPage != null)
            {
                if (errorText != null) errorText.text = message;
                errorPage.SetActive(true);
            }
            else networkPage = RouterWebPages.Message(pageContainer, message);
        }

        private void LoadOnline(string url, float mb, Action completed)
        {
            ClearPage();
            oggleHome.SetActive(false);
            var service = InternetService.Instance;
            var board = system != null ? system.Board : null;
            float delay = 0f;
            string error = null;
            if (service == null || !service.TryTransfer(board, url, mb, out delay, out error))
            {
                ShowError(error ?? "Нет подключения к интернету");
                return;
            }
            // Authored white loading page in Browser.prefab; the rotating image
            // reuses PCOS's exact Loading sprite and the browser's animation.
            if (loadingPage != null)
            {
                if (loadingText != null) loadingText.text = "Загрузка... " + url;
                loadingPage.SetActive(true);
            }
            else networkPage = RouterWebPages.Message(pageContainer, "Загрузка... " + url);
            pendingLoad = StartCoroutine(FinishLoad(delay, completed));
        }

        private IEnumerator FinishLoad(float delay, Action completed)
        {
            float elapsed = 0f;
            while (elapsed < delay)
            {
                InternetService.Instance?.SignalTraffic(system != null ? system.Board : null);
                float step = Mathf.Min(.25f, delay - elapsed);
                yield return new WaitForSecondsRealtime(step);
                elapsed += step;
            }
            pendingLoad = null;
            // A router/power outage mid-transfer must cancel the page.
            var board = system != null ? system.Board : null;
            var status = InternetService.Instance != null ? InternetService.Instance.Status(board) : null;
            if (status == null || status.Quality <= 0f)
            {
                ShowError("Соединение прервано");
                yield break;
            }
            completed();
        }

        public void SearchFromHome()
        {
            if (homeSearchInput == null) return;

            addressBar.text = homeSearchInput.text;
            Search();
        }

        private void ShowResults(string query)
        {
            ClearPage();

            oggleHome.SetActive(false);
            resultsPage.SetActive(true);

            resultsSearchInput.text = query; // показываем текст запроса

            for (int i = resultsContainer.childCount - 1; i >= 0; i--)
                Destroy(resultsContainer.GetChild(i).gameObject);

            foreach (var site in websites)
            {
                if (!site.visible) continue;

                if (Matches(site, query))
                {
                    var capturedSite = site;

                    var result = Instantiate(oggleResultPrefab, resultsContainer);

                    var item = result.GetComponent<OggleResultItem>();
                    item.title.text = site.url;
                    item.description.text = site.description;

                    result.GetComponent<Button>().onClick.AddListener(() =>
                    {
                        OpenSite(capturedSite);
                    });
                }
            }
        }

        // ================= OPEN SITE =================

        /// <summary>
        /// Открыть сайт. Наследники могут вмешаться в этот момент.
        /// </summary>
        /// <remarks>
        /// Точка расширения оставлена virtual: наследник может подменить
        /// поведение перехода, не трогая сам браузер.
        /// </remarks>
        protected virtual void OpenSite(WebsiteItem site)
        {
            if (site == null || site.page == null) return;
            if (RouterWebPages.IsLocalAddress(NormalizeUrl(site.url)))
            {
                ClearPage();
                oggleHome.SetActive(false);
                if (InternetService.Instance == null ||
                     !InternetService.Instance.CanOpenLocalPanel(
                         system != null ? system.Board : null, out var localRouter))
                {
                    ShowError("Сначала подключись к роутеру через Wi-Fi в панели задач PCOS или LAN");
                    return;
                }
                currentPage = Instantiate(site.page, pageContainer);
                currentPage.Init(system);
                return;
            }
            LoadOnline(site.url, Mathf.Max(.1f, site.trafficMB), () =>
            {
                ClearPage();
                oggleHome.SetActive(false);
                currentPage = Instantiate(site.page, pageContainer);
                currentPage.Init(system);
            });
        }

        // ================= CLEAR =================

        private void ClearPage()
        {
            if (loadingPage != null) loadingPage.SetActive(false);
            if (errorPage != null) errorPage.SetActive(false);
            if (pendingLoad != null) { StopCoroutine(pendingLoad); pendingLoad = null; }
            if (networkPage != null) { Destroy(networkPage); networkPage = null; }
            resultsPage.SetActive(false);

            if (currentPage != null)
            {
                Destroy(currentPage.gameObject);
                currentPage = null;
            }
        }

        // ================= MATCH =================

        private bool Matches(WebsiteItem site, string query)
        {
            if (site == null || string.IsNullOrWhiteSpace(query)) return false;
            var terms = query.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var indexed = (site.url ?? "") + " " + (site.description ?? "") + " " +
                string.Join(" ", site.keywords ?? new string[0]);
            foreach (var term in terms)
                if (indexed.IndexOf(term, StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
            return terms.Length > 0;
        }
    }
}