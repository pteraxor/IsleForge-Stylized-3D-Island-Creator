using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IsleForge.Helpers
{
    public struct LabeledValue
    {
        public float Value;
        public string Label;

        public LabeledValue(float value, string label)
        {
            Value = value;
            Label = label;
        }

        public override string ToString()
        {
            return $"{Value:0.00}|{Label}";
        }
    }

    public static class MapDataStore
    {
        public static float[,] IntermediateMap { get; set; }
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

        public static LabeledValue[,] AnnotatedHeightMap { get; set; }
        public static float MaxHeightShare { get; set; }
        public static float MidHeightShare { get; set; }
        public static float LowHeightShare { get; set; }

    }
}
