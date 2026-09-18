using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
    /// <summary>
    /// Управление IPS-панелями: что показывать и откуда брать картинку.
    /// </summary>
    /// <remarks>
    /// Почему отдельное приложение, а не расширение Animator. Animator -- это
    /// редактор кадров для матричных табло: он рисует по клеточкам в жёстком
    /// разрешении вроде 32x16 и отказывается работать с дисплеем другого
    /// размера. Здесь задача обратная -- взять готовый файл (фото, гифку,
    /// анимацию .mov) и вывести его на панель любого разрешения, а сверху
    /// положить показатели компьютера.
    ///
    /// Панель выбирается через тот же DevicePicker, что и остальные устройства,
    /// поэтому она видна в «Моих устройствах» наравне с принтером и табло.
    /// </remarks>
    public class ScreenManager : App
    {
        [SerializeField]
        [Tooltip("Список файлов, которые можно вывести на панель.")]
        private Transform fileParent;

        [SerializeField]
        private Button filePrefab;

        [SerializeField]
        [Tooltip("Предпросмотр выбранного файла.")]
        private RawImage preview;

        [SerializeField]
        private Text statusText;

        [SerializeField]
        [Tooltip("Переключатель режима показа.")]
        private Dropdown modeDropdown;

        [SerializeField]
        [Tooltip("Скорость анимации, кадров в секунду.")]
        private Slider fpsSlider;

        [SerializeField]
        private Text fpsText;

        [SerializeField]
        private Button applyButton;

        /// <summary>Расширения, которые панель умеет показывать.</summary>
        /// <remarks>
        /// .pic -- родной формат картинок игры, .mov -- анимация из Animator,
        /// .gif и .png/.jpg импортируются как обычные изображения.
        /// </remarks>
        private static readonly string[] supported =
            { "pic", "mov", "gif", "png", "jpg", "jpeg" };

        private readonly List<string> files = new List<string>();

        private string selectedPath;
        private Texture2D[] selectedFrames;
        private float fps = 10f;

        public override bool CanOpenExtension(string ext)
        {
            if (string.IsNullOrEmpty(ext)) return false;
            ext = ext.TrimStart('.').ToLowerInvariant();

            for (int i = 0; i < supported.Length; i++)
                if (supported[i] == ext) return true;

            return false;
        }

        protected override void Start()
        {
            base.Start();

            if (fpsSlider != null)
            {
                fpsSlider.minValue = 1f;
                fpsSlider.maxValue = 30f;
                fpsSlider.value = fps;
                fpsSlider.onValueChanged.AddListener(OnFpsChanged);
            }

            if (applyButton != null)
                applyButton.onClick.AddListener(Apply);

            if (modeDropdown != null)
                BuildModeDropdown();

            RefreshFileList();
            UpdateFpsText();
        }

        /// <summary>Заполнить список режимов показа.</summary>
        /// <remarks>
        /// Порядок совпадает с перечислением IpsDisplay.Mode, поэтому индекс
        /// из выпадающего списка приводится к режиму напрямую.
        /// </remarks>
        private void BuildModeDropdown()
        {
            modeDropdown.ClearOptions();
            modeDropdown.AddOptions(new List<string>
            {
                Localization.GetText("Stats on wallpaper"),
                Localization.GetText("Media only"),
                Localization.GetText("Stats on media"),
                Localization.GetText("Stats only")
            });
        }

        public override void Open(string content)
        {
            base.Open(content);

            // ВНИМАНИЕ: сюда приходит СОДЕРЖИМОЕ файла, а не путь -- так
            // устроен App.Open, и Viewer работает так же. Поэтому разбираем
            // содержимое напрямую, а не ищем файл по имени.
            if (string.IsNullOrEmpty(content)) return;

            selectedPath = null;
            selectedFrames = ParseContent(content);
            ShowPreview();
        }

        /// <summary>Собрать список подходящих файлов со всех дисков.</summary>
        private void RefreshFileList()
        {
            files.Clear();

            if (fileParent != null)
            {
                for (int i = fileParent.childCount - 1; i >= 0; i--)
                    Destroy(fileParent.GetChild(i).gameObject);
            }

            var storages = system != null ? system.AllStorage : null;
            if (storages == null) return;

            for (int s = 0; s < storages.Count; s++)
            {
                var storage = storages[s];
                if (storage == null || storage.files == null) continue;

                for (int f = 0; f < storage.files.Count; f++)
                {
                    var file = storage.files[f];
                    if (file == null || file.isFolder || file.hidden) continue;

                    var ext = File.Extension(file.path);
                    if (!CanOpenExtension(ext)) continue;
                    if (files.Contains(file.path)) continue;

                    files.Add(file.path);
                    AddFileButton(file.path);
                }
            }

            if (statusText != null && files.Count == 0)
                statusText.text = Localization.GetText("No suitable files");
        }

        private void AddFileButton(string path)
        {
            if (filePrefab == null || fileParent == null) return;

            var button = Instantiate(filePrefab, fileParent);
            var label = button.GetComponentInChildren<Text>();
            if (label != null) label.text = path;

            string captured = path;
            button.onClick.AddListener(() => SelectFile(captured));
        }

        /// <summary>Загрузить файл и показать его в предпросмотре.</summary>
        private void SelectFile(string path)
        {
            selectedPath = path;
            selectedFrames = LoadFrames(path);
            ShowPreview();
        }

        private void ShowPreview()
        {
            if (selectedFrames == null || selectedFrames.Length == 0)
            {
                if (statusText != null)
                    statusText.text = Localization.GetText("Could not read the file.");
                return;
            }

            if (preview != null)
            {
                preview.texture = selectedFrames[0];
                preview.color = Color.white;
            }

            if (statusText == null) return;

            string label = string.IsNullOrEmpty(selectedPath)
                ? Localization.GetText("Opened file")
                : selectedPath;

            statusText.text = selectedFrames.Length > 1
                ? label + "  (" + selectedFrames.Length + ")"
                : label;
        }

        /// <summary>
        /// Прочитать файл как набор кадров.
        /// </summary>
        /// <remarks>
        /// Анимация (.mov) распаковывается в кадры, всё остальное -- один кадр.
        /// Гифка грузится как обычная картинка: Unity не умеет разбирать её
        /// покадрово без сторонних библиотек, а тащить их ради одного формата
        /// не стоит -- анимацию проще собрать в Animator и сохранить как .mov.
        /// </remarks>
        private Texture2D[] LoadFrames(string path)
        {
            if (system == null || string.IsNullOrEmpty(path)) return null;

            string content;
            if (!system.TryReadFile(path, out content)) return null;

            return ParseContent(content);
        }

        /// <summary>
        /// Разобрать содержимое файла в кадры.
        /// </summary>
        /// <remarks>
        /// Тип определяем по самим данным, а не по расширению: приложение
        /// открывают и двойным кликом по файлу, где путь недоступен. Анимация
        /// .mov -- это несколько картинок в одной строке, всё остальное --
        /// одиночное изображение.
        /// </remarks>
        private Texture2D[] ParseContent(string content)
        {
            if (string.IsNullOrEmpty(content)) return null;

            // StringToMov читает поток двоичных данных и БРОСАЕТ исключение
            // на обычной картинке: там нет ни заголовка, ни счётчика кадров.
            // Поэтому пробуем разобрать как анимацию и спокойно отступаем.
            try
            {
                var mov = FormatConverter.StringToMov(content, false);
                if (mov != null && mov.texs != null && mov.texs.Length > 0)
                {
                    if (mov.interval > 0f) fps = Mathf.Clamp(1f / mov.interval, 1f, 30f);
                    if (fpsSlider != null) fpsSlider.value = fps;
                    return mov.texs;
                }
            }
            catch
            {
                // Это не анимация -- значит одиночная картинка, читаем ниже.
            }

            try
            {
                var tex = FormatConverter.StringToTexture(content);
                return tex != null ? new[] { tex } : null;
            }
            catch
            {
                // Текстовый файл с расширением картинки, битые данные --
                // показывать нечего, но приложение падать не должно.
                return null;
            }
        }

        private void OnFpsChanged(float value)
        {
            fps = Mathf.Max(1f, value);
            UpdateFpsText();
        }

        private void UpdateFpsText()
        {
            if (fpsText != null) fpsText.text = Mathf.RoundToInt(fps) + " FPS";
        }

        /// <summary>Отправить выбранное на панель.</summary>
        public void Apply()
        {
            var os = system;
            var picker = os != null ? os.DevicePicker : null;
            if (picker == null) return;

            picker.PickDevice(os, device =>
            {
                var panel = device as IpsDisplay;
                if (panel == null)
                {
                    // В списке те же устройства, что и у табло, поэтому выбрать
                    // могли матричный дисплей: он это приложение не понимает.
                    if (os != null)
                        os.ShowMessageBox(
                            Localization.GetText("Error"),
                            Localization.GetText("Select an IPS panel."));
                    return;
                }

                StartCoroutine(SendToPanel(panel));
            }, 2);
        }

        private IEnumerator SendToPanel(IpsDisplay panel)
        {
            var os = system;
            var bar = os != null ? os.ProgressBar : null;

            if (bar != null)
            {
                bar.CallProgressBar(Localization.GetText("Uploading"),
                                    new Color(0.3f, 1f, 0.3f, 1f), null);

                float t = 0f;
                while (t < 1f)
                {
                    t += Time.deltaTime * 2f;
                    bar.SetProgress(t);
                    yield return null;
                }

                bar.CloseProgressBar();
            }

            if (panel == null) yield break;

            // Панель должна знать, чьи показатели рисовать.
            if (os != null) panel.Attach(os.Board);

            // Обои берём у системы: режим по умолчанию рисует статистику
            // поверх того же фона, что стоит на рабочем столе.
            var wallpaper = os != null ? os.GetWallpaperTexture() : null;
            if (wallpaper != null) panel.SetWallpaper(wallpaper);

            if (selectedFrames != null && selectedFrames.Length > 0)
                panel.UploadMedia(selectedFrames, 1f / Mathf.Max(1f, fps));

            if (modeDropdown != null)
                panel.SetMode((IpsDisplay.Mode)modeDropdown.value);

            if (statusText != null)
                statusText.text = Localization.GetText("Sent to the panel.");
        }
    }
}
