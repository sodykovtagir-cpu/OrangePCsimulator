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
            if (dlg == null) return;
            int disk = -1;
            var disks = os.AllStorage;
            if (openedFile != null && disks != null)
                for (int n = 0; n < disks.Count; n++)
                    if (disks[n] != null && disks[n].files != null &&
                        disks[n].files.Contains(openedFile)) { disk = n; break; }
            // Save always offers a destination. When editing Autorun.conf or
            // another existing file, start at its actual disk and folder;
            // replacing it requires explicit confirmation in SaveDialog.
			var extensions = new[] { ".txt", ".tmn", ".conf" };
            dlg.ShowDialog(name, content, extensions,
                openedFile != null ? filePath : null, disk, saved =>
                {
                    openedFile = saved;
                    filePath = saved.path;
                }, IsAdministrator);
		}
	}
}
