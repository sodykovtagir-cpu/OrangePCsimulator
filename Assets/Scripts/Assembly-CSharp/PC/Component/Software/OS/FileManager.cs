namespace PC.Component.Software.OS
{
	public class FileManager
	{
		private ComputerSystem cs;

		public FileManager(ComputerSystem cs)
        {
			this.cs = cs;
        }

		public File Write(int storage, string path, string content)
		{
			var system = cs;
			if (system == null) return null;

			var all = system.AllStorage;
			if (all == null || storage < 0 || storage >= all.Count) return null;

			var target = all[storage];
			if (target == null) return null;

			return target.Write(path, content);
		}

		public bool Create(int storage, File file)
		{
			var system = cs;
			if (system == null) return false;

			var all = system.AllStorage;
			if (all == null || storage < 0 || storage >= all.Count) return false;

			var target = all[storage];
			if (target == null) return false;

			return target.AddFile(file);
		}

		public void Delete(int storage, string path)
		{
			var system = cs;
			if (system == null) return;

			var all = system.AllStorage;
			if (all == null || storage < 0 || storage >= all.Count) return;

			var target = all[storage];
			if (target == null) return;

			var files = target.files;
			if (files == null) return;

			for (int i = 0; i < files.Count; i++)
			{
				var f = files[i];
				if (f != null && string.Equals(f.path, path))
				{
					files.RemoveAt(i);
					break;
				}
			}
		}

		public bool Exists(int storage, string path)
		{
			var system = cs;
			if (system == null) return false;

			var all = system.AllStorage;
			if (all == null || storage < 0 || storage >= all.Count) return false;

			var target = all[storage];
			if (target == null) return false;

			return target.ContainsFile(path);
		}

		public bool TryGetFile(int storage, string path, out File file)
		{
			file = null;

			var system = cs;
			if (system == null) return false;

			var all = system.AllStorage;
			if (all == null || storage < 0 || storage >= all.Count) return false;

			var target = all[storage];
			if (target == null) return false;

			return target.TryGetFile(path, out file);
		}
	}
}
