using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace IsleForge.PageStates
{
    public class EdgeEditingPageState
    {
        public WriteableBitmap EditLayer { get; set; }
        public Stack<WriteableBitmap> UndoStack { get; set; } = new();
        public Stack<WriteableBitmap> RedoStack { get; set; } = new();
        public int DrawingToolSize { get; set; }
        public int CurrentEdgeStyle { get; set; }
        public string DrawingMode { get; set; }
        public HashSet<Point> DetectedEdges { get; set; }
        public int EdgeChangesMade { get; set; }
    }
}
