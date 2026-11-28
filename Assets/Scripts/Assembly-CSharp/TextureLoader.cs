using Newtonsoft.Json.Linq;
using UnityEngine;

public class TextureLoader : MonoBehaviour, ISave
{
    [SerializeField]
    private Renderer rend;

    [SerializeField]
    private int matIndex;

    private byte[] data;
    private bool dakimakuraTwoSided;

    private void SetDakimakuraTwoSided(bool value, int width = 1)
    {
        dakimakuraTwoSided = value;
        var uv = GetComponentInChildren<PC.Component.Software.DakimakuraPrintUV>(true);
        if (uv != null) uv.SetTwoSided(value, width);
    }

    public void SetTwoSidedTexture(Texture2D tex, byte[] imageData)
    {
        if (tex == null || rend == null) return;
        SetDakimakuraTwoSided(true, tex.width / 2);
        ApplyTexture(tex, imageData);
    }

    // Imported models may create their renderer in Awake. Bind that renderer
    // before a saved texture is restored or a new purchase is painted.
    public void BindRenderer(Renderer target)
    {
        rend = target;
        matIndex = 0;
    }

    public bool IsEmpty()
    {
        return data == null || data.Length == 0;
    }

    public void CopyFromLoader(TextureLoader loader)
    {
        if (loader == null || loader.data == null)
            return;

        data = (byte[])loader.data.Clone();
        Texture2D tex = FormatConverter.BytesToTexture(data, true);
        if (loader.dakimakuraTwoSided) SetTwoSidedTexture(tex, data);
        else SetTexture(tex, data);
    }

    public void SetTexture(Texture2D tex, byte[] data)
    {
        SetDakimakuraTwoSided(false);
        ApplyTexture(tex, data);
    }

    private void ApplyTexture(Texture2D tex, byte[] data)
    {
        if (tex == null || rend == null)
            return;

        this.data = data;
        Material[] materials = rend.materials;
        if (matIndex >= 0 && matIndex < materials.Length)
        {
            materials[matIndex].mainTexture = tex;
        }
    }

    public void FromData(JObject jObject)
    {
        if (jObject.ContainsKey("dat"))
        {
            JToken jtoken = jObject["dat"];
            byte[] array = System.Convert.FromBase64String(jtoken.ToString());
            Texture2D tex = FormatConverter.BytesToTexture(array, true);
            if (jObject.Value<bool?>("dakimakuraTwoSided") == true)
                SetTwoSidedTexture(tex, array);
            else
                SetTexture(tex, array);
        }
    }

    public void ToData(JObject jObject)
    {
        if (data != null)
        {
            JToken value = System.Convert.ToBase64String(data);
            jObject["dat"] = value;
            if (dakimakuraTwoSided) jObject["dakimakuraTwoSided"] = true;
        }
    }
}
