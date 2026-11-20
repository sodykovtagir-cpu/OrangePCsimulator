using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class CustomPaint : MonoBehaviour, ISave
{
	[SerializeField]
	private Renderer rend;
	private byte[] data;

	/// <summary>Сохранённый .pic — чтобы вещь, пролежавшая в коробке, не потеряла рисунок.</summary>
	public byte[] Data => data;

	/// <summary>Есть ли уже нанесённый рисунок (например, после загрузки сейва).</summary>
	public bool HasDesign => data != null && data.Length > 0;

	private Renderer Target
	{
		get
		{
			if (rend != null) return rend;
			rend = GetComponent<Renderer>();
			if (rend == null) rend = GetComponentInChildren<Renderer>();
			return rend;
		}
	}

	public void SetTexture(Texture2D tex, byte[] data)
    {
        var target = Target;
        if (target == null || tex == null) return;
        // У рендерера без материала .material == null: рисунок всё равно
        // запоминаем, иначе после перезагрузки сейва он потеряется.
        var material = target.material;
        if (material != null) material.mainTexture = tex;
        this.data = data;
    }

	public void FromData(JObject jObject)
    {
        var token = jObject["dat"];
        if (token == null || token.Type == JTokenType.Null) return;
        byte[] dat = Convert.FromBase64String((string)token);
        SetTexture(FormatConverter.BytesToTexture(dat, true), dat);
    }

	public void ToData(JObject jObject)
    {
        if (data == null) return;
        jObject.Add("dat", JToken.FromObject(Convert.ToBase64String(data)));
    }
}
