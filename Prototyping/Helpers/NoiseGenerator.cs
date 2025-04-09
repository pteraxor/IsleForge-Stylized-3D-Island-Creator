using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Prototyping.Helpers
{
    class NoiseGenerator
    {

        private int[] permutation;

        public NoiseGenerator(int? seed = null)
        {
            var random = seed.HasValue ? new Random(seed.Value) : new Random();
            permutation = Enumerable.Range(0, 256).OrderBy(x => random.Next()).ToArray();
            permutation = permutation.Concat(permutation).ToArray();
        }

        // Basic 2D Perlin noise in range [0, 1]
        public float PerlinNoise(float x, float y)
        {
            int xi = (int)Math.Floor(x) & 255;
            int yi = (int)Math.Floor(y) & 255;

            float xf = x - (int)Math.Floor(x);
            float yf = y - (int)Math.Floor(y);

            float u = Fade(xf);
            float v = Fade(yf);

            int aa = permutation[permutation[xi] + yi];
            int ab = permutation[permutation[xi] + yi + 1];
            int ba = permutation[permutation[xi + 1] + yi];
            int bb = permutation[permutation[xi + 1] + yi + 1];

            float x1 = Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1, yf), u);
            float x2 = Lerp(Grad(ab, xf, yf - 1), Grad(bb, xf - 1, yf - 1), u);

            return (Lerp(x1, x2, v) + 1f) / 2f;
        }

        // Fractal noise with octaves
        public float FractalNoise(float x, float y, int octaves, float scale, float persistence, float lacunarity)
        {
            float total = 0f;
            float amplitude = 1f;
            float frequency = 1f;
            float maxValue = 0f;

            for (int i = 0; i < octaves; i++)
            {
                float noiseValue = PerlinNoise(x * scale * frequency, y * scale * frequency);
                total += (noiseValue * 2f - 1f) * amplitude;

                maxValue += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }

            return total / maxValue; // Normalized to roughly [-1, 1]
        }

        private float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);
        private float Lerp(float a, float b, float t) => a + t * (b - a);
        private float Grad(int hash, float x, float y)
        {
            int h = hash & 3;
            float u = h < 2 ? x : y;
            float v = h < 2 ? y : x;
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }
    }
}
