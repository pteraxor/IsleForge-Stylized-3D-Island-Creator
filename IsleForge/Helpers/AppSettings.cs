using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IsleForge.Helpers
{
    public class AppSettings
    {
        public double TextureTiling { get; set; } = 5;
        public int DefaultToolSize { get; set; } = 40; //incorporated
        public float BaseHeight { get; set; } = 12f; //incorporated
        public float MidHeight { get; set; } = 22f; //incorporated
        public float TopHeight { get; set; } = 32f; //incorporated

        public float NoiseStrength { get; set; } = 0.5f; //incorporated
        public float NoiseScale { get; set; } = 0.07f; //incorporated
        public int NoiseOctaves { get; set; } = 4; //incorporated
        public float NoiseLacunarity { get; set; } = 2f; //incorporated
    }

}
