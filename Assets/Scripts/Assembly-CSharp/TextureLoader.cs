using Newtonsoft.Json.Linq;
using UnityEngine;

public class TextureLoader : MonoBehaviour, ISave
{
    [SerializeField]
    private Renderer rend;

    [SerializeField]
    private int matIndex;

    private byte[] data;

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
        SetTexture(tex, data);
    }

    public void SetTexture(Texture2D tex, byte[] data)
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
            SetTexture(tex, array);
        }
    }

    public void ToData(JObject jObject)
    {
        if (data != null)
        {
            JToken value = System.Convert.ToBase64String(data);
            jObject["dat"] = value;
        }
    }
}
