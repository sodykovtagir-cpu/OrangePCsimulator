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
        private Button[] fitButtons;

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

            var barGo = new GameObject("WallpaperFit", typeof(RectTransform));
            barGo.transform.SetParent(transform, false);
            var barRt = barGo.GetComponent<RectTransform>();
            barRt.anchorMin = new Vector2(0f, 1f);
            barRt.anchorMax = new Vector2(1f, 1f);
            barRt.pivot = new Vector2(0.5f, 1f);
            barRt.anchoredPosition = new Vector2(-24f, -218f);
            barRt.sizeDelta = new Vector2(-90f, 26f);

            var layout = barGo.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.padding = new RectOffset(4, 4, 2, 2);

            fitButtons = new Button[fitLabels.Length];
            for (int i = 0; i < fitLabels.Length; i++)
            {
                int mode = i;
                var btnGo = new GameObject(fitLabels[i], typeof(RectTransform), typeof(Image), typeof(Button));
                btnGo.transform.SetParent(barGo.transform, false);
                var img = btnGo.GetComponent<Image>();
                img.color = new Color(0.22f, 0.22f, 0.22f, 0.92f);

                var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
                textGo.transform.SetParent(btnGo.transform, false);
                var textRt = textGo.GetComponent<RectTransform>();
                textRt.anchorMin = Vector2.zero;
                textRt.anchorMax = Vector2.one;
                textRt.offsetMin = Vector2.zero;
                textRt.offsetMax = Vector2.zero;
                var text = textGo.GetComponent<Text>();
                text.font = font;
                text.fontSize = 11;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = Color.white;
                text.text = fitLabels[i];
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;

                var btn = btnGo.GetComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => SetWallpaperFit(mode));
                fitButtons[i] = btn;
            }

            RefreshWallpaperFitBar();
        }

        private void RefreshWallpaperFitBar()
        {
            if (fitButtons == null)
            {
                var bar = transform.Find("WallpaperFit");
                if (bar == null) return;
                fitButtons = bar.GetComponentsInChildren<Button>(true);
            }

            var os = system as GameOS;
            int current = os != null ? os.WallpaperMode : 0;
            for (int i = 0; i < fitButtons.Length; i++)
            {
                if (fitButtons[i] == null) continue;
                var img = fitButtons[i].GetComponent<Image>();
                if (img == null) continue;
                img.color = i == current
                    ? new Color(0.18f, 0.45f, 0.85f, 0.95f)
                    : new Color(0.22f, 0.22f, 0.22f, 0.92f);
            }
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
