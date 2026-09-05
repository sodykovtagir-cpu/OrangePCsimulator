using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Даем псевдоним GameOS, чтобы Unity не путала игровую ОС со стандартной системной
using GameOS = PC.Component.Software.OS.OperatingSystem;

namespace PC.Component.Software
{
    public class Personalization : App
    {
        [SerializeField]
        private Text editPictureText;

        [SerializeField]
        private RawImage userPicture;

        [SerializeField] 
        private GameObject wallpaperDialog;

        [SerializeField]
        private InputField userNameInput;

        [SerializeField]
        private InputField passwordInput;

        private readonly string[] fitLabels = { "Заполнить", "Вписать", "Растянуть", "Центр", "Плитка" };
        private Text fitHeaderLabel;
        private GameObject fitDropdown;
        private GameObject fitList;

        protected override void Start()
        {
            base.Start();
            RefreshPicture();

            var os = system as GameOS;
            if (os == null) return;

            if (userNameInput != null) userNameInput.text = os.UserName;

            var all = os.AllStorage;
            if (all != null && all.Count > 0)
            {
                var st = all[0] as Storage;
                if (st != null && passwordInput != null) passwordInput.text = st.password;
            }

            EnsureWallpaperFitBar();
        }

        private void RefreshPicture()
        {
            var os = system as GameOS;
            if (os == null) return;

            if (userPicture != null) userPicture.texture = os.UserPicture();

            var hasPath = !string.IsNullOrEmpty(os.UserPicturePath);
            if (editPictureText != null) editPictureText.text = Localization.GetText(hasPath ? "Clear" : "Edit");
        }

        // ==========================================
        // ЛОГИКА ОБОЕВ
        // ==========================================

        // ЭТУ ФУНКЦИЮ ПОВЕСИТЬ НА КНОПКУ "Set" РЯДОМ С "Custom background"
        public void SelectCustomBackground()
        {
            var os = system as GameOS;
            if (os == null) return;

            Action<File> cb = file =>
            {
                if (file == null) return;
                // Сохраняем в систему новые обои
                os.SetCustomBackgroundPath(file.path);
            };

            // Открываем проводник для поиска картинок
            os.SelectFile(".pic", cb);
        }

        // 1.8.3 SelectWallpaper button: UpdateBackground(-1, file.path)
        public void CustomBackground()
        {
            var os = system as GameOS;
            if (os == null) return;

            os.SelectFile(".pic", file =>
            {
                if (file == null) return;
                os.UpdateBackground(-1, file.path);
            });
        }

        // Стандартная смена обоев (кликаем по квадратикам внизу)
        public void ChangeBackground(int index)
        {
            var os = system as GameOS;
            if (os == null) return;

            os.UpdateBackground(index);
        }

        public void SetWallpaperFit(int mode)
        {
            var os = system as GameOS;
            if (os == null) return;
            os.WallpaperMode = mode;
            RefreshWallpaperFitBar();
        }

        private void EnsureWallpaperFitBar()
        {
            if (transform.Find("WallpaperFit") != null)
            {
                RefreshWallpaperFitBar();
                return;
            }

            var sample = GetComponentInChildren<Text>(true);
            var font = sample != null ? sample.font : Resources.GetBuiltinResource<Font>("Arial.ttf");

            // Корневой контейнер выпадающего меню.
            var barGo = new GameObject("WallpaperFit", typeof(RectTransform));
            barGo.transform.SetParent(transform, false);
            var barRt = barGo.GetComponent<RectTransform>();
            barRt.anchorMin = new Vector2(0f, 1f);
            barRt.anchorMax = new Vector2(1f, 1f);
            barRt.pivot = new Vector2(0.5f, 1f);
            barRt.anchoredPosition = new Vector2(-24f, -218f);
            barRt.sizeDelta = new Vector2(-90f, 26f);

            // --- Заголовок (кнопка с текущим режимом; клик раскрывает список). ---
            var headerGo = new GameObject("Header", typeof(RectTransform), typeof(Image), typeof(Button));
            headerGo.transform.SetParent(barGo.transform, false);
            var headerRt = headerGo.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.sizeDelta = new Vector2(0f, 26f);
            headerRt.anchoredPosition = Vector2.zero;
            var headerImg = headerGo.GetComponent<Image>();
            headerImg.color = new Color(0.22f, 0.22f, 0.22f, 0.95f);

            var headerTextGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            headerTextGo.transform.SetParent(headerGo.transform, false);
            var htr = headerTextGo.GetComponent<RectTransform>();
            htr.anchorMin = Vector2.zero; htr.anchorMax = Vector2.one;
            htr.offsetMin = new Vector2(8f, 0f); htr.offsetMax = new Vector2(-22f, 0f);
            fitHeaderLabel = headerTextGo.GetComponent<Text>();
            fitHeaderLabel.font = font;
            fitHeaderLabel.fontSize = 12;
            fitHeaderLabel.alignment = TextAnchor.MiddleLeft;
            fitHeaderLabel.color = Color.white;
            fitHeaderLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            fitHeaderLabel.verticalOverflow = VerticalWrapMode.Overflow;
            fitHeaderLabel.raycastTarget = false;

            var arrowGo = new GameObject("Arrow", typeof(RectTransform), typeof(Text));
            arrowGo.transform.SetParent(headerGo.transform, false);
            var ar = arrowGo.GetComponent<RectTransform>();
            ar.anchorMin = new Vector2(1f, 0f); ar.anchorMax = new Vector2(1f, 1f);
            ar.pivot = new Vector2(1f, 0.5f);
            ar.sizeDelta = new Vector2(18f, 0f);
            ar.anchoredPosition = new Vector2(-6f, 0f);
            var arrow = arrowGo.GetComponent<Text>();
            arrow.font = font; arrow.fontSize = 12;
            arrow.alignment = TextAnchor.MiddleCenter; arrow.color = Color.white;
            arrow.text = "▼"; arrow.raycastTarget = false;

            // --- Выпадающий список (раскрывается вниз от заголовка). ---
            fitList = new GameObject("List", typeof(RectTransform), typeof(Image));
            fitList.transform.SetParent(barGo.transform, false);
            var listRt = fitList.GetComponent<RectTransform>();
            listRt.anchorMin = new Vector2(0f, 1f);
            listRt.anchorMax = new Vector2(1f, 1f);
            listRt.pivot = new Vector2(0.5f, 1f);
            listRt.sizeDelta = new Vector2(0f, 24f * fitLabels.Length + 4f);
            listRt.anchoredPosition = new Vector2(0f, -28f);
            fitList.GetComponent<Image>().color = new Color(0.16f, 0.16f, 0.16f, 0.98f);
            var vlg = fitList.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 0f; vlg.padding = new RectOffset(2, 2, 2, 2);
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            var fitter = fitList.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            for (int i = 0; i < fitLabels.Length; i++)
            {
                int mode = i;
                var optGo = new GameObject("Opt_" + fitLabels[i], typeof(RectTransform), typeof(Image), typeof(Button));
                optGo.transform.SetParent(fitList.transform, false);
                var ort = optGo.GetComponent<RectTransform>();
                ort.sizeDelta = new Vector2(0f, 24f);
                var optImg = optGo.GetComponent<Image>();
                optImg.color = new Color(1f, 1f, 1f, 0.02f);

                var tGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
                tGo.transform.SetParent(optGo.transform, false);
                var trt = tGo.GetComponent<RectTransform>();
                trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
                trt.offsetMin = new Vector2(8f, 0f); trt.offsetMax = Vector2.zero;
                var t = tGo.GetComponent<Text>();
                t.font = font; t.fontSize = 12;
                t.alignment = TextAnchor.MiddleLeft; t.color = Color.white;
                t.text = fitLabels[i];
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                t.raycastTarget = false;

                var btn = optGo.GetComponent<Button>();
                btn.targetGraphic = optImg;
                var colors = btn.colors;
                colors.highlightedColor = new Color(0.18f, 0.45f, 0.85f, 0.9f);
                colors.normalColor = Color.white;
                btn.colors = colors;
                btn.onClick.AddListener(() => { SetWallpaperFit(mode); SetDropdownOpen(false); });
            }

            headerGo.GetComponent<Button>().onClick.AddListener(() => SetDropdownOpen(fitList == null || !fitList.activeSelf));
            fitDropdown = barGo;
            SetDropdownOpen(false);
            RefreshWallpaperFitBar();
        }

        private void SetDropdownOpen(bool open)
        {
            if (fitList != null) fitList.SetActive(open);
        }

        private void RefreshWallpaperFitBar()
        {
            var os = system as GameOS;
            int current = os != null ? Mathf.Clamp(os.WallpaperMode, 0, fitLabels.Length - 1) : 0;
            if (fitHeaderLabel != null && current < fitLabels.Length)
                fitHeaderLabel.text = fitLabels[current];
        }

        // ==========================================

        public void EditPicture()
        {
            var os = system as GameOS;
            if (os == null) return;

            var path = os.UserPicturePath;
            if (string.IsNullOrEmpty(path))
            {
                Action<File> cb = file =>
                {
                    if (file == null) return;
                    os.UserPicturePath = file.path;
                    RefreshPicture();
                };
                os.SelectFile(".pic", cb);
            }
            else
            {
                os.UserPicturePath = "";
                RefreshPicture();
            }
        }

        public void EditUserName()
        {
            var i = userNameInput;
            if (i == null) return;
            i.interactable = true;
            i.ActivateInputField();
        }

        public void OpenWallpaperDialog()
        {
            if (wallpaperDialog != null)
                wallpaperDialog.SetActive(true);
        }

        public void SelectWallpaperFromSystem()
        {
            var os = system as GameOS;
            if (os == null) return;

            os.SelectFile(".pic", file =>
            {
                if (file == null) return;

                os.SetCustomBackgroundPath(file.path);

                if (wallpaperDialog != null)
                    wallpaperDialog.SetActive(false);
            });
        }

        public void CloseWallpaperDialog()
        {
            if (wallpaperDialog != null)
                wallpaperDialog.SetActive(false);
        }

        public void OnEndEditUserName(string name)
        {
            var os = system as GameOS;
            if (os == null) return;
            var input = userNameInput;

            if (string.IsNullOrEmpty(name))
            {
                if (input != null) input.text = os.UserName;
            }
            else
            {
                os.UserName = name;
            }

            StartCoroutine(DisableInput(input));
        }

        public void EditPassword()
        {
            var i = passwordInput;
            if (i == null) return;
            i.interactable = true;
            i.ActivateInputField();
        }

        public void OnEndEditPassword(string password)
        {
            var os = system as GameOS;
            if (os == null) return;
            var all = os.AllStorage;
            if (all == null || all.Count == 0) return;
            var st = all[0] as Storage;
            if (st == null) return;
            st.password = password;
            StartCoroutine(DisableInput(passwordInput));
        }

        public void SelectWallpaperFromDevice()
        {
            var os = system as GameOS;
            if (os == null)
                return;

            NativeGallery.GetImageFromGallery(path =>
            {
                if (string.IsNullOrEmpty(path))
                    return;

                byte[] bytes = System.IO.File.ReadAllBytes(path);

                os.ImportWallpaperFromDevice(bytes);

            }, "Выберите изображение", "image/*");
        }

        private IEnumerator DisableInput(InputField input)
        {
            yield return new WaitForEndOfFrame();
            if (input == null) yield break;
            input.interactable = false;
        }
    }
}
