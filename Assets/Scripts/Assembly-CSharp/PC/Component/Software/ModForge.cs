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

    private File selectedFile;
    private int selectedProduct;

    private void Start()
    {
        foreach (Text text in texts)
        {
            text.text = Item.TranslateBracket(text.text);
        }
    }

    public void SelectFile()
    {
        os.SelectFile(".pic", (file) =>
        {
            if (file == null) return;
            selectedFile = file;
            purchaseButton.interactable = true;
            fileNameText.text = file.path;
        });
    }

    public void Purchase()
    {
        if (selectedFile == null) return;
        int price = prices[selectedProduct];
        if (Main.Instance.Money < price)
        {
            Main.Instance.FadeText("<color=red>"+Localization.GetText("Not enough cash")+"</color>");
            return;
        }

        Main.Instance.Spend(price);
        Spawn(coverPrefabs[selectedProduct], FormatConverter.StringToTexture(selectedFile.content));
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
        // update price label
        int price = prices[index];
        string pricestr = price.ToString() + "$";
        priceText.text = pricestr;
        home.SetActive(false);
        checkout.SetActive(true);
        thankYou.SetActive(false);
    }
}