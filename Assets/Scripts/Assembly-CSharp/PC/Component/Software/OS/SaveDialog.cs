using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software.OS
{
	public class SaveDialog : MonoBehaviour
	{
		[SerializeField]
		private InputField fileNameInput;

		[SerializeField]
		private OperatingSystem system;

		[SerializeField]
		private Text extensionText;

		private string content;

		private string[] extensions;

		private int index;

		public void ShowDialog(string fileName, string content, string[] extensions)
		{
			this.content = content;
			this.extensions = extensions;
			index = 0;
			if (extensions == null || extensions.Length == 0) return;
			if (extensionText != null) extensionText.text = extensions[0];
			if (fileNameInput != null) fileNameInput.text = fileName;
			var go = gameObject;
			if (go != null) go.SetActive(true);
		}

		public void NextExtension()
		{
			var exts = extensions;
			index = index + 1;
			if (exts == null) return;
			var count = exts.Length;
			if (index >= count) index = 0;
			if (extensionText != null && index >= 0 && index < count) extensionText.text = exts[index];
		}

		public void Save()
		{
			var input = fileNameInput;
			var exts = extensions;
			if (input == null || exts == null) return;

			int i = index;
			if (i < 0 || i >= exts.Length) return;

			var path = input.text + exts[i];
			var file = new File(path, content, false, 0);

			var sys = system;
			var fm = sys != null ? sys.FileManager : null;
			if (fm != null)
			{
				fm.Create(0, file);
				var go = gameObject;
				if (go != null) go.SetActive(false);
			}
		}

		public void Cancel()
		{
			var go = gameObject;
			if (go != null) go.SetActive(false);
		}
	}
}
