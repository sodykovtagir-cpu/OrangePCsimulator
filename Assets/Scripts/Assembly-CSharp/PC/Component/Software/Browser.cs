using System;
using UnityEngine;
using UnityEngine.UI;

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
            string query = addressBar.text.ToLower();

            if (string.IsNullOrEmpty(query))
                return;

            ShowResults(query);
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

        private void OpenSite(WebsiteItem site)
        {
            ClearPage();

            oggleHome.SetActive(false);

            var instance = Instantiate(site.page, pageContainer);
            currentPage = instance;
            currentPage.Init(system);
        }

        // ================= CLEAR =================

        private void ClearPage()
        {
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
            if (site.url.ToLower().Contains(query))
                return true;

            if (site.keywords != null)
            {
                foreach (var word in site.keywords)
                {
                    if (word.ToLower().Contains(query))
                        return true;
                }
            }

            return false;
        }
    }
}