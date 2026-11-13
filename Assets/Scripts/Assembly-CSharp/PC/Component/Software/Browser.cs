using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using PC.Component.Networking;
using System.Collections.Generic;

namespace PC.Component.Software
{
    public partial class Browser : App
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

        [Header("Oggle home (authored prefab)")]
        [SerializeField] private RectTransform historyContent;
        [SerializeField] private RectTransform favoritesPanel;
        [SerializeField] private GameObject addFavoritesPanel;
        [SerializeField] private InputField favoriteAddressInput;
        [SerializeField] private Text addFavoritesTitle;
        [SerializeField] private GameObject historyItemPrefab;
        [SerializeField] private GameObject favoritesItemPrefab;
        [SerializeField] private Sprite fallbackSiteIcon;

        public void SearchFromResults()
        {
            if (resultsSearchInput == null) return;

            addressBar.text = resultsSearchInput.text;  // синхронизируем верхнюю строку
            Search();
        }

        private Website currentPage;
        private GameObject networkPage;
        private Coroutine pendingLoad;
        private const string RecentSitesPath = "System/oggle_recent.json";
        private const string FavoritesPath = "System/oggle_favorites.json";
        private const int MaxRecentSites = 5;
        private const string TariffsUrl = "www.typolink.com/tariffs";
        private const string RouterUrl = "192.168.1.1";
        [Serializable]
        private sealed class RecentSites
        {
            public List<string> urls = new List<string>();
        }
        private readonly List<string> recentUrls = new List<string>();
        private readonly List<string> favoriteUrls = new List<string>();
        private readonly List<GameObject> historyItems = new List<GameObject>();
        private readonly List<GameObject> favoriteItems = new List<GameObject>();
        private bool recentLoaded;
        private bool favoritesLoaded;

        protected override void Start()
        {
            base.Start();
            if (!localNavigationStarted) ShowHome();
        }

        // ================= HOME =================

        public void ShowHome()
        {
            ClearPage();
            if (oggleHome == null) return;
            oggleHome.SetActive(true);
            LoadRecentSites();
            LoadFavorites();
            RenderRecentSites();
            RenderFavorites();
        }

        private void LoadRecentSites()
        {
            if (recentLoaded || system == null || system.FileManager == null) return;
            recentLoaded = true;
            if (!system.FileManager.TryGetFile(0, RecentSitesPath, out var saved) ||
                saved == null || string.IsNullOrEmpty(saved.content) || saved.content.Length > 8192) return;
            try
            {
                var data = JsonUtility.FromJson<RecentSites>(saved.content);
                if (data == null || data.urls == null) return;
                foreach (var url in data.urls)
                {
                    var normalized = NormalizeUrl(url);
                    if (ValidSiteAddress(normalized) && !recentUrls.Contains(normalized))
                        recentUrls.Add(normalized);
                    if (recentUrls.Count == MaxRecentSites) break;
                }
            }
            catch (ArgumentException) { /* Keep the PC's unrelated files intact. */ }
        }

        private void LoadFavorites()
        {
            if (favoritesLoaded || system == null || system.FileManager == null) return;
            favoritesLoaded = true;
            if (!system.FileManager.TryGetFile(0, FavoritesPath, out var saved) || saved == null)
            {
                // Seed only a new PC. An existing empty file means the player
                // intentionally deleted both defaults: never re-add them.
                favoriteUrls.Clear();
                favoriteUrls.Add(TariffsUrl);
                favoriteUrls.Add(RouterUrl);
                if (!SaveUrls(FavoritesPath, favoriteUrls)) favoritesLoaded = false;
                return;
            }
            if (string.IsNullOrEmpty(saved.content) || saved.content.Length > 8192) return;
            try
            {
                var data = JsonUtility.FromJson<RecentSites>(saved.content);
                if (data == null || data.urls == null) return;
                foreach (var url in data.urls)
                {
                    var normalized = NormalizeUrl(url);
                    if (ValidSiteAddress(normalized) && !favoriteUrls.Contains(normalized))
                        favoriteUrls.Add(normalized);
                    if (favoriteUrls.Count == MaxRecentSites) break;
                }
            }
            catch (ArgumentException) { /* Corrupt favorites must not damage other PC files. */ }
        }

        private bool SaveUrls(string path, List<string> urls)
        {
            if (system == null || system.FileManager == null) return false;
            var payload = JsonUtility.ToJson(new RecentSites { urls = new List<string>(urls) });
            var saved = system.FileManager.Write(0, path, payload);
            if (saved == null || saved.content != payload) return false;
            saved.hidden = true;
            return true;
        }

        private bool SaveRecentSites() { return SaveUrls(RecentSitesPath, recentUrls); }

        private static bool ValidSiteAddress(string address)
        {
            if (string.IsNullOrEmpty(address) || address.Length > 255 ||
                address.IndexOf('.') < 0) return false;
            foreach (char c in address)
                if (char.IsWhiteSpace(c) || char.IsControl(c)) return false;
            return true;
        }

        private void RememberSite(string url)
        {
            LoadRecentSites();
            var normalized = NormalizeUrl(url);
            if (!ValidSiteAddress(normalized)) return;
            recentUrls.Remove(normalized);
            recentUrls.Insert(0, normalized);
            if (recentUrls.Count > MaxRecentSites)
                recentUrls.RemoveRange(MaxRecentSites, recentUrls.Count - MaxRecentSites);
            SaveRecentSites();
        }

        private static void ClearItems(List<GameObject> items)
        {
            foreach (var item in items)
                if (item != null) { item.SetActive(false); Destroy(item); }
            items.Clear();
        }

        private void RenderRecentSites()
        {
            ClearItems(historyItems);
            if (historyContent == null || historyItemPrefab == null) return;
            foreach (var url in recentUrls)
            {
                var row = Instantiate(historyItemPrefab, historyContent);
                row.name = "History: " + url;
                var label = row.GetComponentInChildren<Text>();
                if (label != null) label.text = url;
                var icon = row.transform.Find("Image");
                if (icon != null)
                {
                    var image = icon.GetComponent<Image>();
                    if (image != null)
                    {
                        image.sprite = SiteIcon(url);
                        image.preserveAspect = true;
                    }
                }
                // The author's history row has an Image but no Button.
                var button = row.GetComponent<Button>() ?? row.AddComponent<Button>();
                button.targetGraphic = row.GetComponent<Image>();
                var capturedUrl = url;
                button.onClick.AddListener(() => OpenRecentSite(capturedUrl));
                historyItems.Add(row);
            }
        }

        private Sprite SiteIcon(string url)
        {
            if (websites != null)
                foreach (var site in websites)
                    if (site != null && site.page != null &&
                        NormalizeUrl(site.url) == NormalizeUrl(url) && site.page.icon != null)
                        return site.page.icon;
            return fallbackSiteIcon;
        }

        private void RenderFavorites()
        {
            ClearItems(favoriteItems);
            if (favoritesPanel == null || favoritesItemPrefab == null) return;
            foreach (var url in favoriteUrls)
            {
                var tile = Instantiate(favoritesItemPrefab, favoritesPanel);
                tile.name = "Favorite: " + url;
                var item = tile.GetComponent<FavoriteTile>();
                if (item != null)
                    item.Initialize(url, SiteIcon(url), OpenRecentSite, RemoveFavorite);
                favoriteItems.Add(tile);
            }
        }

        private void RemoveFavorite(string url)
        {
            LoadFavorites();
            var previous = new List<string>(favoriteUrls);
            if (!favoriteUrls.Remove(url)) return;
            if (!SaveUrls(FavoritesPath, favoriteUrls))
            {
                favoriteUrls.Clear();
                favoriteUrls.AddRange(previous);
                Debug.LogWarning("Oggle: не удалось сохранить удаление избранного на диск ПК");
                return;
            }
            RenderFavorites();
        }

        private void OpenRecentSite(string url)
        {
            var normalized = NormalizeUrl(url);
            if (websites != null)
                foreach (var site in websites)
                    if (site != null && site.page != null &&
                        NormalizeUrl(site.url) == normalized)
                    { OpenSite(site); return; }
            if (RouterWebPages.IsLocalAddress(normalized))
            { ShowNetworkPage(normalized); return; }
            ShowError("Сайт " + normalized + " пока недоступен в Oggle");
        }

        public void CloseAddSiteDialog()
        {
            if (addFavoritesPanel != null) addFavoritesPanel.SetActive(false);
        }

        public void OpenAddSiteDialog()
        {
            if (addFavoritesPanel == null || favoriteAddressInput == null) return;
            LoadFavorites();
            favoriteAddressInput.text = "";
            if (addFavoritesTitle != null) addFavoritesTitle.text = "Add Favorites";
            addFavoritesPanel.SetActive(true);
            favoriteAddressInput.ActivateInputField();
        }

        public void AddFavorite()
        {
            if (favoriteAddressInput == null) return;
            var url = NormalizeUrl(favoriteAddressInput.text);
            if (!ValidSiteAddress(url))
            {
                if (addFavoritesTitle != null) addFavoritesTitle.text = "Укажи адрес сайта с точкой";
                return;
            }
            LoadFavorites();
            var previous = new List<string>(favoriteUrls);
            favoriteUrls.Remove(url);
            favoriteUrls.Insert(0, url);
            if (favoriteUrls.Count > MaxRecentSites)
                favoriteUrls.RemoveRange(MaxRecentSites, favoriteUrls.Count - MaxRecentSites);
            if (!SaveUrls(FavoritesPath, favoriteUrls))
            {
                favoriteUrls.Clear();
                favoriteUrls.AddRange(previous);
                if (addFavoritesTitle != null) addFavoritesTitle.text = "Не удалось сохранить на диск ПК";
                return;
            }
            CloseAddSiteDialog();
            RenderFavorites();
        }

        // ================= SEARCH =================

        public void Search()
        {
            if (addressBar == null) return;
            string query = addressBar.text.Trim();
            if (string.IsNullOrEmpty(query)) return;
            if (query.StartsWith("file:", StringComparison.OrdinalIgnoreCase)) { OpenLocalAddress(query); return; }
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
            RememberSite(url);
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
                RememberSite(site.url);
                return;
            }
            LoadOnline(site.url, Mathf.Max(.1f, site.trafficMB), () =>
            {
                ClearPage();
                oggleHome.SetActive(false);
                currentPage = Instantiate(site.page, pageContainer);
                currentPage.Init(system);
                RememberSite(site.url);
            });
        }

        // ================= CLEAR =================

        private void ClearPage()
        {
            ClearLocalContext();
            CloseAddSiteDialog();
            if (loadingPage != null) loadingPage.SetActive(false);
            if (errorPage != null) errorPage.SetActive(false);
            if (pendingLoad != null) { StopCoroutine(pendingLoad); pendingLoad = null; }
            if (networkPage != null) { networkPage.SetActive(false); Destroy(networkPage); networkPage = null; }
            resultsPage.SetActive(false);

            if (currentPage != null)
            {
                currentPage.gameObject.SetActive(false);
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