using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
	public class LuaSyntaxHighlight : MonoBehaviour
	{
		[SerializeField] private InputField code;
		[Tooltip("Отдельный Text поверх поля (Rich Text). Само поле лучше сделать почти прозрачным.")]
		[SerializeField] private Text overlay;
		[SerializeField] private Color keyword = new Color(0.35f, 0.2f, 0.75f);
		[SerializeField] private Color str = new Color(0.15f, 0.45f, 0.2f);
		[SerializeField] private Color comment = new Color(0.4f, 0.45f, 0.4f);
		[SerializeField] private Color number = new Color(0.15f, 0.35f, 0.65f);

		static readonly Regex Token = new Regex(
			@"(--[^\n]*)|(""(?:\\.|[^""])*"")|('(?:\\.|[^'])*')|(\b(?:and|break|do|else|elseif|end|false|for|function|if|in|local|nil|not|or|repeat|return|then|true|until|while)\b)|(\b\d+(?:\.\d+)?\b)",
			RegexOptions.Compiled);

		public void Bind(InputField field)
		{
			code = field;
			if (code != null)
			{
				code.onValueChanged.RemoveListener(OnChanged);
				code.onValueChanged.AddListener(OnChanged);
			}
			Refresh();
		}

		void OnEnable()
		{
			if (code != null)
			{
				code.onValueChanged.RemoveListener(OnChanged);
				code.onValueChanged.AddListener(OnChanged);
			}
			Refresh();
		}

		void OnDisable()
		{
			if (code != null) code.onValueChanged.RemoveListener(OnChanged);
		}

		void OnChanged(string _) { Refresh(); }

		public void Refresh()
		{
			if (overlay == null || code == null) return;
			overlay.supportRichText = true;
			overlay.raycastTarget = false;
			overlay.text = Colorize(code.text ?? "");
		}

		string Colorize(string src)
		{
			var sb = new StringBuilder(src.Length * 2);
			int last = 0;
			foreach (Match m in Token.Matches(src))
			{
				if (m.Index > last) sb.Append(Escape(src.Substring(last, m.Index - last)));
				string col;
				if (m.Groups[1].Success) col = Hex(comment);
				else if (m.Groups[2].Success || m.Groups[3].Success) col = Hex(str);
				else if (m.Groups[4].Success) col = Hex(keyword);
				else col = Hex(number);
				sb.Append("<color=#").Append(col).Append(">").Append(Escape(m.Value)).Append("</color>");
				last = m.Index + m.Length;
			}
			if (last < src.Length) sb.Append(Escape(src.Substring(last)));
			return sb.ToString();
		}

		static string Escape(string s)
		{
			return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
		}

		static string Hex(Color c)
		{
			return ColorUtility.ToHtmlStringRGBA(c);
		}
	}
}
