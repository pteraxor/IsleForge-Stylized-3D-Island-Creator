using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace IsleForge.PageStates
{
    public class BaseMapPageState
    {
        public WriteableBitmap Bitmap { get; set; }
        public Stack<WriteableBitmap> UndoStack { get; set; } = new();
        public Stack<WriteableBitmap> RedoStack { get; set; } = new();
        public double CanvasCoverage { get; set; }

        // Optional: add tool config
        public int DrawingToolSize { get; set; }
        public int CurrentLayer { get; set; }
        public string DrawingMode { get; set; }
        public string StampShape { get; set; }
        public bool RestrictToBaseLayer { get; set; }
    }
}
