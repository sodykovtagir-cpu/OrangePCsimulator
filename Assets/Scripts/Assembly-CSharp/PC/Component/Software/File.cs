using System;
using UnityEngine;

namespace PC.Component.Software
{
	[Serializable]
	public class File
	{
		public string path;

		[TextArea(1, 5)]
		public string content;

		public bool hidden;

		public int size;

		public int StorageSize {
		    get
			{
				if (size == 0) size = content.Length;
				return size;
			}
		}

		public File(string path, string content = "", bool hidden = false, int size = 0)
        {
			this.path = path;
			this.content = content;
			this.hidden = hidden;
			this.size = size;
        }

		public string NameWithoutExtension()
		{
			return NameWithoutExtension(path);
		}

		public string Extension()
		{
			return Extension(path);
		}

		public static string NameWithoutExtension(string path)
		{
			if (path == null) return path;
			int i = path.LastIndexOf('.');
			return i != -1 ? path.Substring(0, i) : path;
		}

		public static string Extension(string path)
		{
			if (string.IsNullOrEmpty(path)) return "";
			int i = path.LastIndexOf('.');
			return i != -1 ? path.Substring(i) : "";
		}
	}
}
