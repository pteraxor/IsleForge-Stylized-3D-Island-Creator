using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Prototyping.Helpers
{
    public static class MapDataStore
    {
        public static float[,] BaseLayer { get; set; }
        public static float[,] MidLayer { get; set; }
        public static float[,] TopLayer { get; set; }
        public static float[,] EdgeLayer { get; set; }

        public static float[,] FootPrint { get; set; }

        public static float[,] TopMidEdges { get; set; }
        public static float[,] TopBaseEdges { get; set; }
        public static float[,] MidBaseEdges { get; set; }
        public static float[,] BottomBaseEdges { get; set; }

        public static float[,] FinalHeightMap { get; set; }

    }

}
