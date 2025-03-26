using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Prototyping.Tools
{
    public class FreehandTool : IDrawingTool
    {
        public Func<Point, bool> Mask { get; set; } = _ => true;

        private Point _lastPoint;
        private bool _isDrawing;
        private Color _color;
        private int _brushSize;

        public FreehandTool(Color color, int brushSize)
        {
            _color = color;
            _brushSize = brushSize;
        }

        public void OnMouseDown(Point position, WriteableBitmap bitmap)
        {
            _isDrawing = true;
            _lastPoint = position;
            DrawDot(bitmap, position);
            System.Diagnostics.Debug.WriteLine("OnMouseDown");
        }

        public void OnMouseMove(Point position, WriteableBitmap bitmap)
        {
            if (!_isDrawing) return;

            DrawLine(bitmap, _lastPoint, position);
            _lastPoint = position;
        }

        public void OnMouseUp(Point position, WriteableBitmap bitmap)
        {
            _isDrawing = false;
        }

        private void DrawDot(WriteableBitmap bitmap, Point p)
        {
            System.Diagnostics.Debug.WriteLine($"Drawing dot at {p}");

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
                            ChangePixel(px, py, bitmap);
                            //bitmap.SetPixel(px, py, _color);
                        }
                    }
                }
            }
        }


        private void DrawLine(WriteableBitmap bitmap, Point from, Point to)
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
                DrawDot(bitmap, new Point(x0, y0));

                if (x0 == x1 && y0 == y1) break;

                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x0 += sx; }
                if (e2 < dx) { err += dx; y0 += sy; }
            }
        }

        //moving the part that actually changes the pixel here, so that other parts of logic can be added
        private void ChangePixel(int px, int py, WriteableBitmap bitmap)
        {
            //bitmap.SetPixel(px, py, _color);
            if (Mask(new Point(px, py)))
            {
                bitmap.SetPixel(px, py, _color);
            }
        }
    }
}
