using System.Collections.Generic;

namespace PC.Component.Software
{
    // Some legacy apps edit Storage.files/File fields directly rather than using the
    // file manager. A lightweight snapshot catches those changes too, without hashing
    // image contents, serializing the disk or rebuilding icons every frame.
    internal sealed class DesktopFileSnapshot
    {
        private struct Entry
        {
            public File file;
            public string path;
            public string content;
            public bool hidden;
            public bool isFolder;
            public int size;

            public Entry(File file)
            {
                this.file = file;
                path = file != null ? file.path : null;
                content = file != null ? file.content : null;
                hidden = file != null && file.hidden;
                isFolder = file != null && file.isFolder;
                size = file != null ? file.size : 0;
            }

            public bool Matches(File current)
            {
                if (!ReferenceEquals(file, current)) return false;
                return current == null || (path == current.path && hidden == current.hidden &&
                    isFolder == current.isFolder && size == current.size &&
                    ReferenceEquals(content, current.content));
            }
        }

        private readonly List<Entry> entries = new List<Entry>();
        private IList<File> source;
        private bool captured;

        public bool HasChanged(IList<File> files)
        {
            if (!captured || !ReferenceEquals(source, files)) return true;
            int count = files != null ? files.Count : 0;
            if (count != entries.Count) return true;
            for (int i = 0; i < count; i++)
                if (!entries[i].Matches(files[i])) return true;
            return false;
        }

        public void Capture(IList<File> files)
        {
            entries.Clear();
            source = files;
            if (files != null)
                for (int i = 0; i < files.Count; i++) entries.Add(new Entry(files[i]));
            captured = true;
        }
    }
}
