using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;

namespace IsleForge.Helpers
{
    public static class BitmapManager
    {
        private static readonly Dictionary<string, WriteableBitmap> _bitmaps = new Dictionary<string, WriteableBitmap>();

        public static void Set(string key, WriteableBitmap bitmap)
        {
            if (key == null || bitmap == null)
                return;

            _bitmaps[key] = bitmap;
        }

        public static WriteableBitmap Get(string key)
        {
            WriteableBitmap bmp;
            return _bitmaps.TryGetValue(key, out bmp) ? bmp : null;
        }

        public static bool ContainsBitmap(string key)
        {
            return _bitmaps.ContainsKey(key);
        }

        public static void RemoveBitmap(string key)
        {
            if (_bitmaps.ContainsKey(key))
                _bitmaps.Remove(key);
        }

        public static void ClearAll()
        {
            _bitmaps.Clear();
        }

        public static IEnumerable<string> GetAllBitmapNames()
        {
            return _bitmaps.Keys;
        }

        public static WriteableBitmap ResizeBitmap(WriteableBitmap source, int width, int height)
        {
            var scaled = new TransformedBitmap(source, new ScaleTransform(
                width / (double)source.PixelWidth,
                height / (double)source.PixelHeight,
                0, 0));

            var target = new WriteableBitmap(scaled);

            return target;
        }

        public static void SaveBitmapToFile(string key, string filePath)
        {
            if (!ContainsBitmap(key))
            {
                MessageBox.Show($"Bitmap '{key}' not found.", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var bitmap = Get(key);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            using (FileStream stream = new FileStream(filePath, FileMode.Create))
            {
                encoder.Save(stream);
            }

            MessageBox.Show($"Bitmap '{key}' saved to:\n{filePath}", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

    }
}
