using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using PC.Component.Networking;
using System.Collections.Generic;

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
        private const string RecentSitesPath = "System/oggle_recent.json";
        private const int MaxRecentSites = 5;
        [Serializable]
        private sealed class RecentSites
        {
            public List<string> urls = new List<string>();
        }
        private readonly List<string> recentUrls = new List<string>();
        private RectTransform recentPanel;
        private RectTransform recentRows;
        private GameObject addSiteDialog;
        private bool recentLoaded;

        protected override void Start()
        {
            base.Start();
            ShowHome();
        }

        // ================= HOME =================

        public void ShowHome()
        {
            ClearPage();
            if (oggleHome == null) return;
            oggleHome.SetActive(true);
            LoadRecentSites();
            RenderRecentSites();
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

        private bool SaveRecentSites()
        {
            if (system == null || system.FileManager == null) return false;
            var payload = JsonUtility.ToJson(new RecentSites { urls = new List<string>(recentUrls) });
            var saved = system.FileManager.Write(0, RecentSitesPath, payload);
            if (saved == null || saved.content != payload) return false;
            saved.hidden = true;
            return true;
        }

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

        private static RectTransform Box(Transform parent, string name, Vector2 size, Vector2 position,
            Color? background = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            if (background.HasValue) go.AddComponent<Image>().color = background.Value;
            return rect;
        }

        private static Text Caption(Transform parent, string value, Vector2 size, Vector2 position,
            int fontSize = 14, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var rect = Box(parent, "Text", size, position);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.color = Color.black;
            label.alignment = alignment;
            label.text = value;
            label.raycastTarget = false;
            return label;
        }

        private static Button ActionButton(Transform parent, string name, Vector2 size,
            Vector2 position, Action click)
        {
            var rect = Box(parent, name, size, position,
                new Color(.88f, .93f, .97f, 1f));
            var button = rect.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => click());
            Caption(rect, name, size, Vector2.zero, 13, TextAnchor.MiddleCenter);
            return button;
        }

        private void RenderRecentSites()
        {
            if (oggleHome == null) return;
            if (recentPanel == null)
            {
                recentPanel = Box(oggleHome.transform, "Recent sites", new Vector2(450, 124),
                    new Vector2(0, -98), new Color(1f, 1f, 1f, .96f));
                Caption(recentPanel, "Недавние сайты", new Vector2(260, 25),
                    new Vector2(-85, 44), 15);
                ActionButton(recentPanel, "+ Добавить", new Vector2(96, 26),
                    new Vector2(167, 44), OpenAddSiteDialog);
                recentRows = Box(recentPanel, "Recent links", new Vector2(430, 95),
                    new Vector2(0, -14));
            }
            for (int i = recentRows.childCount - 1; i >= 0; i--)
            {
                var oldRow = recentRows.GetChild(i).gameObject;
                oldRow.SetActive(false);
                Destroy(oldRow);
            }
            for (int i = 0; i < recentUrls.Count; i++)
            {
                string url = recentUrls[i];
                var title = url.Length > 52 ? url.Substring(0, 49) + "..." : url;
                ActionButton(recentRows, title, new Vector2(425, 18),
                    new Vector2(0, 33 - i * 18), () => OpenRecentSite(url));
            }
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

        private void CloseAddSiteDialog()
        {
            if (addSiteDialog == null) return;
            Destroy(addSiteDialog);
            addSiteDialog = null;
        }

        private void OpenAddSiteDialog()
        {
            if (addSiteDialog != null) return;
            var overlay = Box(transform, "Add site overlay", Vector2.zero, Vector2.zero,
                new Color(0f, 0f, 0f, .30f));
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            addSiteDialog = overlay.gameObject;
            overlay.gameObject.AddComponent<Button>().onClick.AddListener(CloseAddSiteDialog);
            var modal = Box(overlay, "Add site dialog", new Vector2(390, 184), Vector2.zero,
                new Color(.94f, .96f, .98f, 1f));
            WindowChrome.Apply(modal);
            Caption(modal, "Добавить сайт", new Vector2(345, 30), new Vector2(0, 65), 18);
            Caption(modal, "Адрес сайта", new Vector2(345, 24), new Vector2(0, 26));
            var field = Box(modal, "Website address", new Vector2(345, 32),
                new Vector2(0, -5), Color.white);
            var input = field.gameObject.AddComponent<InputField>();
            var text = Caption(field, "", new Vector2(325, 28), Vector2.zero);
            input.textComponent = text;
            input.characterLimit = 255;
            input.text = "";
            var warning = Caption(modal, "", new Vector2(345, 22), new Vector2(0, -37), 12);
            ActionButton(modal, "Добавить", new Vector2(108, 30), new Vector2(56, -68), () =>
            {
                var url = NormalizeUrl(input.text);
                if (!ValidSiteAddress(url))
                { warning.text = "Укажи адрес сайта с точкой"; return; }
                LoadRecentSites();
                var previous = new List<string>(recentUrls);
                recentUrls.Remove(url);
                recentUrls.Insert(0, url);
                if (recentUrls.Count > MaxRecentSites)
                    recentUrls.RemoveRange(MaxRecentSites, recentUrls.Count - MaxRecentSites);
                if (!SaveRecentSites())
                {
                    recentUrls.Clear();
                    recentUrls.AddRange(previous);
                    warning.text = "Не удалось сохранить на диск ПК";
                    return;
                }
                CloseAddSiteDialog();
                RenderRecentSites();
            });
            ActionButton(modal, "Отмена", new Vector2(105, 30), new Vector2(-67, -68),
                CloseAddSiteDialog);
            input.ActivateInputField();
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
            CloseAddSiteDialog();
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