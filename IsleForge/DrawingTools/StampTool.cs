using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IsleForge.DrawingTools
{
    public class StampTool : IDrawingTool
    {
        public Func<Point, bool> Mask { get; set; } = _ => true;

        private string _shape;
        private int _size;
        private Color _color;

        public StampTool(string shape, int size, Color color)
        {
            _shape = shape;
            _size = size;
            _color = color;
        }

        public void OnMouseDown(Point position, WriteableBitmap bitmap)
        {
            // Only stamp if point is valid under the mask
            if (!Mask(position)) return;

            switch (_shape)
            {
                case "Circle":
                    DrawCircle((int)position.X, (int)position.Y, _size, bitmap);
                    break;
                case "Hexagon":
                    DrawPolygon(position, _size, 6, bitmap);
                    break;
                case "Square":
                    DrawPolygon(position, _size, 4, bitmap);
                    break;
                case "Triangle":
                    DrawPolygon(position, _size, 3, bitmap);
                    break;
            }
        }

        public void OnMouseMove(Point position, WriteableBitmap bitmap) { }

        public void OnMouseUp(Point position, WriteableBitmap bitmap) { }

        private void DrawCircle(int cx, int cy, int radius, WriteableBitmap bitmap)
        {
            using (bitmap.GetBitmapContext())
            {
                for (int y = -radius; y <= radius; y++)
                {
                    for (int x = -radius; x <= radius; x++)
                    {
                        if (x * x + y * y <= radius * radius)
                        {
                            int px = cx + x;
                            int py = cy + y;

                            if (px >= 0 && px < bitmap.PixelWidth &&
                                py >= 0 && py < bitmap.PixelHeight &&
                                Mask(new Point(px, py)))
                            {
                                bitmap.SetPixel(px, py, _color);
                            }
                        }
                    }
                }
            }
        }

        private void DrawPolygon(Point center, int size, int sides, WriteableBitmap bitmap)
        {
            PointCollection points = new PointCollection();
            double angleStep = 2 * Math.PI / sides;

            // Apply rotation based on shape type
            double rotationOffset = 0;
            if (sides == 4) // Square
                rotationOffset = Math.PI / 4; // 45 degrees
            else if (sides == 3) // Triangle
                rotationOffset = -Math.PI / 2; // Point up

            for (int i = 0; i < sides; i++)
            {
                double angle = angleStep * i + rotationOffset;
                double x = center.X + size * Math.Cos(angle);
                double y = center.Y + size * Math.Sin(angle);
                points.Add(new Point(x, y));
            }

            using (bitmap.GetBitmapContext())
            {
                for (int y = (int)(center.Y - size); y <= (int)(center.Y + size); y++)
                {
                    List<int> intersections = new List<int>();

                    for (int i = 0; i < points.Count; i++)
                    {
                        Point p1 = points[i];
                        Point p2 = points[(i + 1) % points.Count];

                        if ((p1.Y <= y && p2.Y > y) || (p2.Y <= y && p1.Y > y))
                        {
                            double slope = (p2.X - p1.X) / (p2.Y - p1.Y);
                            int intersectX = (int)(p1.X + (y - p1.Y) * slope);
                            intersections.Add(intersectX);
                        }
                    }

                    intersections.Sort();

                    for (int i = 0; i < intersections.Count; i += 2)
                    {
                        if (i + 1 < intersections.Count)
                        {
                            for (int x = intersections[i]; x <= intersections[i + 1]; x++)
                            {
                                if (x >= 0 && x < bitmap.PixelWidth &&
                                    y >= 0 && y < bitmap.PixelHeight &&
                                    Mask(new Point(x, y)))
                                {
                                    bitmap.SetPixel(x, y, _color);
                                }
                            }
                        }
                    }
                }
            }
        }


        private void DrawPolygon1(Point center, int size, int sides, WriteableBitmap bitmap)
        {
            PointCollection points = new PointCollection();
            double angleStep = 360.0 / sides;

            //add rotation for the shapes with strange orientations at 0
            double rotationOffset = 0;
            if (sides == 4) // Square{
            {
                rotationOffset = Math.PI / 4; // 45 degrees
            }               
            else if (sides == 3) // Triangle
            {
                rotationOffset = -Math.PI / 2; // Point up
            }            



            for (int i = 0; i < sides; i++)
            {
                double angle = angleStep * i + rotationOffset;
                double x = center.X + size * Math.Cos(angle);
                double y = center.Y + size * Math.Sin(angle);
                points.Add(new Point(x, y));
            }

            using (bitmap.GetBitmapContext())
            {
                for (int y = (int)(center.Y - size); y <= (int)(center.Y + size); y++)
                {
                    List<int> intersections = new List<int>();

                    for (int i = 0; i < points.Count; i++)
                    {
                        Point p1 = points[i];
                        Point p2 = points[(i + 1) % points.Count];

                        if ((p1.Y <= y && p2.Y > y) || (p2.Y <= y && p1.Y > y))
                        {
                            double slope = (p2.X - p1.X) / (p2.Y - p1.Y);
                            int intersectX = (int)(p1.X + (y - p1.Y) * slope);
                            intersections.Add(intersectX);
                        }
                    }

                    intersections.Sort();

                    for (int i = 0; i < intersections.Count; i += 2)
                    {
                        if (i + 1 < intersections.Count)
                        {
                            for (int x = intersections[i]; x <= intersections[i + 1]; x++)
                            {
                                if (x >= 0 && x < bitmap.PixelWidth &&
                                    y >= 0 && y < bitmap.PixelHeight &&
                                    Mask(new Point(x, y)))
                                {
                                    bitmap.SetPixel(x, y, _color);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
