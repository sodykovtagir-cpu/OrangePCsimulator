using UnityEngine;
using UnityEngine.UI;
using PC.Component.Software;

public class ModForge : Website
{
    [SerializeField] private Text fileNameText;
    [SerializeField] private Text priceText;

    [SerializeField] private GameObject home;
    [SerializeField] private GameObject checkout;
    [SerializeField] private GameObject thankYou;

    [SerializeField] private Button purchaseButton;

    [SerializeField] private Text[] texts;
    [SerializeField] private CustomPaint[] coverPrefabs;
    [SerializeField] private int[] prices;

    [Header("HD (доплата)")]
    [Tooltip("Галочка «HD»: снимает ограничение на размер рисунка и добавляет 500$ к цене.")]
    [SerializeField] private Toggle hdToggle;
    [SerializeField] private int hdSurcharge = 500;

    [Header("3D-превью крышки на корпусе (назначь в инспекторе)")]
    [Tooltip("RawImage на чекауте, куда рендерится вращающаяся крышка.")]
    [SerializeField] private RawImage previewImage;
    [Tooltip("Камера, которая снимает крышку. Должна смотреть на превью-сцену (при желании поставь у неё Culling Mask, чтобы видеть только слой превью).")]
    [SerializeField] private Camera previewCamera;
    [Tooltip("Пустой трансформ в мире — точка, куда ставится крышка для превью (можно вне комнаты, например над монитором).")]
    [SerializeField] private Transform previewStage;
    [Tooltip("Скорость авто-вращения крышки (градусов/сек).")]
    [SerializeField] private float previewSpinSpeed = 40f;

    private File selectedFile;
    private int selectedProduct;

    private GameObject previewInstance;
    private RenderTexture previewRT;
    private bool previewActive;
    private Texture2D previewTexture;

    private void Start()
    {
        foreach (Text text in texts)
        {
            text.text = Item.TranslateBracket(text.text);
        }
    }

    private void Update()
    {
        if (!previewActive || previewInstance == null) return;
        // Авто-вращение крышки для обзора (модель крышки сама не крутится в игре).
        previewInstance.transform.Rotate(0f, previewSpinSpeed * Time.deltaTime, 0f, Space.World);

        if (previewCamera != null && previewRT != null)
            previewCamera.Render();
    }

    private void OnDestroy()
    {
        StopPreview();
        ReleasePreviewTexture();
    }

    public void SelectFile()
    {
        os.SelectFile(".pic", (file) =>
        {
            if (file == null) return;
            selectedFile = file;
            purchaseButton.interactable = true;
            fileNameText.text = file.path;
            StartPreview();
        });
    }

    public void Purchase()
    {
        if (selectedFile == null) return;

        int price = CurrentPrice();
        var m = Main.Instance;
        if (m == null) return;

        var tex = FormatConverter.StringToTexture(selectedFile.content);
        if (tex == null) return;

        // Доплата HD снимает ограничение на размер рисунка: крупные .pic
        // принимаются только с галочкой HD.
        if (!Hd && (tex.width > 512 || tex.height > 512))
        {
            if (priceText != null) priceText.text = "HD required";
            tex.Apply(false, true);
            return;
        }

        if (m.Money < price)
        {
            m.FadeText("<color=red>" + Localization.GetText("Not enough cash") + "</color>");
            tex.Apply(false, true);
            return;
        }

        m.Spend(price);
        Spawn(coverPrefabs[selectedProduct], tex);
        StopPreview();
        ReleasePreviewTexture();
        checkout.SetActive(false);
        thankYou.SetActive(true);
    }

    private void Spawn(CustomPaint prefab, Texture2D texture)
    {
        if (prefab == null || texture == null || Main.Instance == null) return;
        byte[] bytes = texture.EncodeToPNG();
        texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
        var go = Main.Instance.InstantDelivery(prefab.gameObject);
        if (go == null) return;
        CustomPaint item = go.GetComponent<CustomPaint>();
        if (item != null) item.SetTexture(texture, bytes);
    }

    public void SelectProduct(int index)
    {
        selectedProduct = index;
        if (priceText != null) priceText.text = CurrentPrice().ToString() + "$";
        home.SetActive(false);
        checkout.SetActive(true);
        thankYou.SetActive(false);

        // Если файл уже выбран — пересобираем превью под новую модель крышки.
        if (selectedFile != null) StartPreview();
    }

    // Вызывается галочкой HD в инспекторе (Toggle.onValueChanged -> SetHd),
    // чтобы цена обновлялась сразу.
    public void SetHd(bool on)
    {
        if (priceText != null) priceText.text = CurrentPrice().ToString() + "$";
    }

    private bool Hd => hdToggle != null && hdToggle.isOn;
    private int BasePrice => (prices != null && selectedProduct >= 0 && selectedProduct < prices.Length) ? prices[selectedProduct] : 0;
    private int CurrentPrice() => BasePrice + (Hd ? hdSurcharge : 0);

    #region Превью крышки на корпусе

    private void StartPreview()
    {
        StopPreview();
        ReleasePreviewTexture();

        if (coverPrefabs == null || selectedProduct < 0 || selectedProduct >= coverPrefabs.Length) return;
        var prefab = coverPrefabs[selectedProduct];
        if (prefab == null) return;

        var tex = FormatConverter.StringToTexture(selectedFile != null ? selectedFile.content : null);
        if (tex == null) return;
        previewTexture = tex;

        // Инстанцируем модель крышки (ATX/ITX × стекло/защита зависит от выбранного товара)
        // в превью-точке.
        var stage = previewStage != null ? previewStage : transform;
        previewInstance = Instantiate(prefab.gameObject, stage);
        previewInstance.transform.localPosition = Vector3.zero;
        previewInstance.transform.localRotation = Quaternion.identity;
        previewInstance.transform.localScale = Vector3.one;

        var cp = previewInstance.GetComponent<CustomPaint>();
        if (cp != null)
        {
            // Рендерим рисунок на крышку (как при покупке), но данные не сохраняем — это превью.
            cp.SetTexture(tex, tex.EncodeToPNG());
        }

        // Прячем всё лишнее, что могло попасть в превью-объект (логика доставки/сохранения тут не нужна).
        previewActive = true;

        EnsureRenderTarget();
    }

    private void EnsureRenderTarget()
    {
        if (previewImage == null || previewCamera == null) return;

        const int size = 256;
        previewRT = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
        previewRT.antiAliasing = 4;
        previewRT.Create();

        previewCamera.targetTexture = previewRT;
        previewCamera.enabled = false; // рендерим вручную в Update (после поворота)
        previewImage.texture = previewRT;
        previewImage.color = Color.white;
    }

    private void StopPreview()
    {
        previewActive = false;
        if (previewInstance != null)
        {
            Destroy(previewInstance);
            previewInstance = null;
        }
        if (previewCamera != null) previewCamera.targetTexture = null;
    }

    private void ReleasePreviewTexture()
    {
        if (previewRT != null)
        {
            previewRT.Release();
            Destroy(previewRT);
            previewRT = null;
        }
        if (previewTexture != null)
        {
            // Текстуру превью в игру не забираем (покупка создаёт свою) — освобождаем.
            previewTexture.Apply(false, true);
            previewTexture = null;
        }
    }

    #endregion
}
