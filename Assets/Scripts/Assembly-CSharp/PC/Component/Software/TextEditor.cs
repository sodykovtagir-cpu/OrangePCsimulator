using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
	public class TextEditor : App
	{
		[SerializeField]
		private InputField input;

		private string filePath;

		public override void Open(string content)
		{
			base.Open(content);
			var i = input;
			if (i != null) i.text = string.IsNullOrEmpty(content) ? "" : content;
			filePath = "Untitled" + ".txt";
		}

		public void OpenFile()
		{
			var o = system;
			if (o == null) return;

			System.Action<File> cb = file =>
			{
				if (file == null) return;
				var i = input;
				if (i != null) i.text = file.content;
				filePath = file.path;
			};

			o.SelectFile("*", cb);
		}

		public void Save()
		{
			var os = system;
			if (os == null) return;
			var dlg = os.SaveDialog;
			if (dlg == null) return;

			var name = File.NameWithoutExtension(filePath);
			var i = input;
			if (i == null) return;
			var content = i.text;

			var extensions = new[] { ".txt", ".tmn" };
			dlg.ShowDialog(name, content, extensions);
		}
	}
}
