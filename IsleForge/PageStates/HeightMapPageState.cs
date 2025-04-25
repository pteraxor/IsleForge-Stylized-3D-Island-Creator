using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IsleForge.Helpers;
using System.Windows.Media.Imaging;

namespace IsleForge.PageStates
{
    public class HeightMapPageState
    {
        public WriteableBitmap HeightMapLayer { get; set; }
        public LabeledValue[,] SolvedMap { get; set; }
        public float TopHeight { get; set; }
        public float MidHeight { get; set; }
        public float BaseHeight { get; set; }
        public bool HasMapBeenCreated { get; set; }
    }

}
