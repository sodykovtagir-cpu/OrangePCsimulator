using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;

public static class Localization
{
	private static IDictionary<string, string> content;

	private static string language;

	private static IDictionary<string, string> Content => null;

	public static event Action LanguageChanged;

	public static string GetText(string key)
	{
		return GetText(key, language);
	}

	public static string GetText(string key, string lang){
		var text=GetLocalFile();
		if(string.IsNullOrEmpty(text)||content==null)return key;
		var lines=text.Split(new[]{"\r\n","\n"},StringSplitOptions.None);
		if(lines.Length<2)return key;
		var headerCols=lines[0].Split('\t');
		if(headerCols.Length<2)return key;
		int langIndex=-1;
		for(int i=1;i<headerCols.Length;i++)if(headerCols[i]==lang){langIndex=i;break;}
		if(langIndex<1)return key;
		if(!content.TryGetValue(key,out var allLangValues))return key;
		var values=allLangValues.Split('\t');
		int valueIndex=langIndex-1;
		if(valueIndex>=values.Length)return key;
		var result=values[valueIndex];
		return string.IsNullOrEmpty(result)?key:result;
	}

	public static string[] GetRow(string key)
	{
		if (content.TryGetValue(key, out string v) && v != null)
		{
			return v.Split('\t', StringSplitOptions.None);
		}
		return new string[0];
	}

	public static string GetLanguage()
	{
		return language;
	}

	public static void SetLanguage(string language)
	{
		Localization.language = language;
		LanguageChanged?.Invoke();
	}

	public static string[] GetAllLanguages()
	{
		return GetRow("*Short Form");
	}

	public static void CreateContent()
	{
		var text = GetLocalFile();
		if (string.IsNullOrEmpty(text)) return;
		if (content == null) content = new Dictionary<string, string>();
		else content.Clear();
		var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
		for (int i = 0; i < lines.Length; i++)
		{
			if (string.IsNullOrEmpty(lines[i])) continue;
			var cols = lines[i].Split('\t');
			if (cols.Length < 2 || string.IsNullOrEmpty(cols[0])) continue;
			var val = string.Join("\t", cols, 1, cols.Length - 1);
			if (string.IsNullOrEmpty(val)) continue;
			content.Add(cols[0], val);
		}
	}

	public static string GetLocalFile()
	{
		var textAsset = Resources.Load<TextAsset>("Translate");
		return textAsset.text;
	}
}
