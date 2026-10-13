using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
	public class TextEditor : App
	{
		[SerializeField]
		private InputField input;

		private string filePath;
        private File openedFile;

        public override bool CanOpenExtension(string ext) =>
            string.Equals(ext, ".conf", System.StringComparison.OrdinalIgnoreCase);

        public void OpenDocument(File file)
        {
            if (file == null) return;
            openedFile = file;
            if (input != null) input.text = file.content ?? "";
            filePath = file.path;
        }

		public override void Open(string content)
		{
			base.Open(content);
			var i = input;
			if (i != null) i.text = string.IsNullOrEmpty(content) ? "" : content;
			filePath = "Untitled" + ".txt";
            openedFile = null;
		}

		public void OpenFile()
		{
			var o = system;
			if (o == null) return;

			System.Action<File> cb = file =>
			{
				if (file == null) return;
                OpenDocument(file);
			};

			o.SelectFile("*", cb);
		}

		public void Save()
		{
			var os = system;
			if (os == null) return;
			var dlg = os.SaveDialog;

			var name = File.NameWithoutExtension(filePath);
			var i = input;
			if (i == null) return;
			var content = i.text;
            // Editing an existing file must update that path, not create
            // Untitled.txt; in particular System/Autorun.conf is boot-critical.
            if (openedFile != null)
            {
                int disk = -1;
                var disks = os.AllStorage;
                if (disks != null)
                    for (int index = 0; index < disks.Count; index++)
                        if (disks[index] != null && disks[index].files != null
                            && disks[index].files.Contains(openedFile)) { disk = index; break; }
                var saved = disk >= 0 && os.FileManager != null
                    ? os.FileManager.Write(disk, filePath, content) : null;
                if (saved == null || saved.content != content)
                    os.ShowMessageBox("Text Editor", "Could not save " + filePath);
                return;
            }

            if (dlg == null) return;
			var extensions = new[] { ".txt", ".tmn", ".conf" };
			dlg.ShowDialog(name, content, extensions);
		}
	}
}
