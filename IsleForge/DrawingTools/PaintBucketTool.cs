using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IsleForge.DrawingTools
{
    public class PaintBucketTool : IDrawingTool
    {
        public Func<Point, bool> Mask { get; set; } = _ => true;

        private Color _fillColor;

        public PaintBucketTool(Color color)
        {
            _fillColor = color;
        }

        public void OnMouseDown(Point position, WriteableBitmap bitmap)
        {
            int x = (int)position.X;
            int y = (int)position.Y;

            //check if in bounds and check if in mask
            if (!InBounds(x, y, bitmap))
            {
                return;
            }               
            if (!Mask(position))
            {
                return;
            } 
            
            // Get the color of the pixel we're about to fill from
            Color targetColor = bitmap.GetPixel(x, y);
            // If the color is already what we want to fill with, do nothin
            if (ColorsAreEqual(targetColor, _fillColor))
            {
                return;
            }                           
            // Start flood fill
            FloodFill(bitmap, x, y, targetColor);
        }

        //these are not really going to be used as much here
        public void OnMouseMove(Point position, WriteableBitmap bitmap) { }

        public void OnMouseUp(Point position, WriteableBitmap bitmap) { }

        //Breadth-First Search Flood Fill algorithm
        private void FloodFill(WriteableBitmap bitmap, int startX, int startY, Color targetColor)
        {
            Queue<Point> queue = new Queue<Point>();
            queue.Enqueue(new Point(startX, startY));

            using (bitmap.GetBitmapContext())
            {
                while (queue.Count > 0)
                {
                    Point p = queue.Dequeue();
                    int x = (int)p.X;
                    int y = (int)p.Y;

                    if (!InBounds(x, y, bitmap)) continue;
                    if (!Mask(p)) continue;

                    Color current = bitmap.GetPixel(x, y);
                    if (!ColorsAreEqual(current, targetColor)) continue;

                    //fill the pixel with a new color
                    bitmap.SetPixel(x, y, _fillColor);

                    //add neighboring pixels to the queue
                    queue.Enqueue(new Point(x + 1, y));
                    queue.Enqueue(new Point(x - 1, y));
                    queue.Enqueue(new Point(x, y + 1));
                    queue.Enqueue(new Point(x, y - 1));
                }
            }
        }

        private bool InBounds(int x, int y, WriteableBitmap bmp)
        {
            return x >= 0 && y >= 0 && x < bmp.PixelWidth && y < bmp.PixelHeight;
        }

        private bool ColorsAreEqual(Color a, Color b)
        {
            return a.R == b.R && a.G == b.G && a.B == b.B && a.A == b.A;
        }
    }
}
