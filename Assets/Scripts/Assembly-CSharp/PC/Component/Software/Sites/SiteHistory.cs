using System;
using System.Collections.Generic;
using System.Text;

namespace PC.Component.Software.Sites
{
    /// <summary>Transactional undo/redo with a byte budget, including asset imports.</summary>
    public sealed class SiteHistory
    {
        const int Budget = 16 * 1024 * 1024;
        readonly List<string> undo = new List<string>(), redo = new List<string>();
        int bytes;
        public SiteDocument Document { get; private set; }
        public bool CanUndo => undo.Count > 0;
        public bool CanRedo => redo.Count > 0;
        public SiteHistory(SiteDocument document) { Reset(document); }
        public void Reset(SiteDocument document)
        {
            Document = SiteDocument.Parse(document.ToJson()); undo.Clear(); redo.Clear(); bytes = 0;
        }
        public void Change(Action<SiteDocument> edit)
        {
            string before = Document.ToJson();
            var candidate = SiteDocument.Parse(before);
            edit(candidate);
            string after = candidate.ToJson();
            if (before == after) return;
            undo.Add(before); bytes += Encoding.UTF8.GetByteCount(before);
            foreach (string item in redo) bytes -= Encoding.UTF8.GetByteCount(item);
            redo.Clear(); Document = candidate; Trim();
        }
        public void Undo() { Swap(undo, redo); }
        public void Redo() { Swap(redo, undo); }
        void Swap(List<string> from, List<string> to)
        {
            if (from.Count == 0) return;
            string current = Document.ToJson(), target = from[from.Count - 1];
            var parsed = SiteDocument.Parse(target);
            from.RemoveAt(from.Count - 1); bytes -= Encoding.UTF8.GetByteCount(target);
            to.Add(current); bytes += Encoding.UTF8.GetByteCount(current); Document = parsed; Trim();
        }
        void Trim()
        {
            while ((undo.Count + redo.Count > 40 || bytes > Budget) && undo.Count > 0)
            { bytes -= Encoding.UTF8.GetByteCount(undo[0]); undo.RemoveAt(0); }
            while (bytes > Budget && redo.Count > 0)
            { bytes -= Encoding.UTF8.GetByteCount(redo[0]); redo.RemoveAt(0); }
        }
    }
}
