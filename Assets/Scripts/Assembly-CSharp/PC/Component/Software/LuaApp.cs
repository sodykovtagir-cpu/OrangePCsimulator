using System;
using PC.Component.Software.Lua;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
	public class LuaApp : App
	{
		[SerializeField] private Text titleText;
		[SerializeField] private Image chrome;
		[SerializeField] private RectTransform contentRoot;
		[SerializeField] private Vector2 defaultWindowSize = new Vector2(420, 280);

		PcosLua vm;
		LuaUi ui;

		public override bool SingleInstance => false;
		protected override bool ShowMenuBar => false;

		public void ApplyChrome(Color color)
		{
			if (chrome != null) chrome.color = color;
			var img = GetComponent<Image>();
			if (img != null) img.color = color;
		}

		public void SetTitle(string title)
		{
			if (titleText != null) titleText.text = title ?? "Lua";
		}

		public void SetWindowSize(float w, float h)
		{
			SetDefaultSize(new Vector2(Mathf.Clamp(w, 160, 1200), Mathf.Clamp(h, 120, 800)));
		}

		public override void Open(string content)
		{
			base.Open(content);
			rect = GetComponent<RectTransform>();
			if (chrome == null) chrome = GetComponent<Image>();
			if (titleText == null)
			{
				var t = transform.Find("Title");
				if (t != null) titleText = t.GetComponent<Text>();
			}
			EnsureContent();
			var pack = LuaAppPackage.Parse(content);
			if (pack == null) pack = new LuaAppPackage { name = "Lua", script = content ?? "" };
			SetTitle(pack.name);
			SetWindowSize(defaultWindowSize.x, defaultWindowSize.y);
			if (ui != null) ui.Clear();
			vm = new PcosLua();
			vm.Printer = line => Debug.Log("[LuaApp] " + line);
			PcosLuaHost.Bind(vm, system);
			BindWindowApi();
			var font = titleText != null ? titleText.font : null;
			ui = new LuaUi(contentRoot, font, vm);
			ui.Bind();
			try { vm.DoString(pack.script ?? ""); }
			catch (Exception ex)
			{
				if (system != null) system.ShowMessageBox(pack.name ?? "Lua", ex.Message);
			}
		}

		void EnsureContent()
		{
			if (contentRoot != null) return;
			var go = new GameObject("Content", typeof(RectTransform), typeof(Image));
			go.transform.SetParent(transform, false);
			contentRoot = go.GetComponent<RectTransform>();
			contentRoot.anchorMin = Vector2.zero;
			contentRoot.anchorMax = Vector2.one;
			contentRoot.offsetMin = new Vector2(6, 6);
			contentRoot.offsetMax = new Vector2(-6, -36);
			go.GetComponent<Image>().color = new Color(0.63f, 0.83f, 0.82f, 1f);
		}

		public override void Close()
		{
			if (ui != null) ui.Clear();
			vm = null;
			ui = null;
			base.Close();
		}
	}
}
