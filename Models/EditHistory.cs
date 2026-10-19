using System.Collections.Generic;
using System.Windows.Media.Imaging;

namespace PhotoEditorApp.Models
{
    public class EditHistory
    {
        private readonly Stack<BitmapSource> _undoStack;
        private readonly int _maxHistoryCount;

        public EditHistory(int maxHistoryCount = 10)
        {
            _undoStack = new Stack<BitmapSource>();
            _maxHistoryCount = maxHistoryCount;
        }

        public void Push(BitmapSource state)
        {
            if (state == null) return;
            if (_undoStack.Count >= _maxHistoryCount)
            {
                var items = new List<BitmapSource>(_undoStack);
                items.RemoveAt(items.Count - 1);
                _undoStack.Clear();
                for (int i = items.Count - 1; i >= 0; i--) _undoStack.Push(items[i]);
            }
            _undoStack.Push(state);
        }

        public BitmapSource Pop() => _undoStack.Count > 0 ? _undoStack.Pop() : null;
        public bool CanUndo => _undoStack.Count > 0;
        public void Clear() => _undoStack.Clear();
    }
}
