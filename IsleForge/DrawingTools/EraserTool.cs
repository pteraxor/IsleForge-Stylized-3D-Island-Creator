using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IsleForge.DrawingTools
{
    public class EraserTool : IDrawingTool
    {
        public Func<Point, bool> Mask { get; set; } = _ => true;

        private Point _lastPoint;
        private bool _isErasing;
        private int _brushSize;

        public EraserTool(int brushSize)
        {
            _brushSize = brushSize;
        }

        public void OnMouseDown(Point position, WriteableBitmap bitmap)
        {
            _isErasing = true;
            _lastPoint = position;
            EraseDot(bitmap, position);
        }

        public void OnMouseMove(Point position, WriteableBitmap bitmap)
        {
            if (!_isErasing) return;

            EraseLine(bitmap, _lastPoint, position);
            _lastPoint = position;
        }

        public void OnMouseUp(Point position, WriteableBitmap bitmap)
        {
            _isErasing = false;
        }

        private void EraseDot(WriteableBitmap bitmap, Point p)
        {
            int radius = _brushSize;

            using (bitmap.GetBitmapContext())
            {
                for (int y = -radius; y <= radius; y++)
                {
                    for (int x = -radius; x <= radius; x++)
                    {
                        int px = (int)p.X + x;
                        int py = (int)p.Y + y;

                        if (px >= 0 && px < bitmap.PixelWidth &&
                            py >= 0 && py < bitmap.PixelHeight &&
                            x * x + y * y <= radius * radius)
                        {
                            ErasePixel(px, py, bitmap);
                        }
                    }
                }
            }
        }

        private void EraseLine(WriteableBitmap bitmap, Point from, Point to)
        {
            int x0 = (int)from.X;
            int y0 = (int)from.Y;
            int x1 = (int)to.X;
            int y1 = (int)to.Y;

            int dx = Math.Abs(x1 - x0), dy = Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                EraseDot(bitmap, new Point(x0, y0));
                if (x0 == x1 && y0 == y1) break;

                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x0 += sx; }
                if (e2 < dx) { err += dx; y0 += sy; }
            }
        }

        private void ErasePixel(int px, int py, WriteableBitmap bitmap)
        {
            if (Mask(new Point(px, py)))
            {
                bitmap.SetPixel(px, py, Colors.Transparent);
            }
        }
    }
}
