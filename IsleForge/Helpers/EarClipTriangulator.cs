using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IsleForge.Helpers
{
    public struct Point2D
    {
        public double X, Y;
        public Point2D(double x, double y)
        {
            X = x;
            Y = y;
        }
    }

    public struct Triangle2D
    {
        public Point2D A, B, C;
        public Triangle2D(Point2D a, Point2D b, Point2D c)
        {
            A = a;
            B = b;
            C = c;
        }
    }

    public static class EarClipTriangulator
    {
        public static List<Triangle2D> Triangulate(List<Point2D> polygon)
        {
            var triangles = new List<Triangle2D>();
            if (polygon.Count < 3) return triangles;

            var verts = new List<Point2D>(polygon);

            // Ensure CCW
            if (SignedArea(verts) < 0)
                verts.Reverse();

            while (verts.Count > 3)
            {
                bool earFound = false;
                for (int i = 0; i < verts.Count; i++)
                {
                    Point2D prev = verts[(i - 1 + verts.Count) % verts.Count];
                    Point2D curr = verts[i];
                    Point2D next = verts[(i + 1) % verts.Count];

                    if (!IsConvex(prev, curr, next))
                        continue;

                    bool isEar = true;
                    for (int j = 0; j < verts.Count; j++)
                    {
                        if (j == (i - 1 + verts.Count) % verts.Count || j == i || j == (i + 1) % verts.Count)
                            continue;
                        if (PointInTriangle(verts[j], prev, curr, next))
                        {
                            isEar = false;
                            break;
                        }
                    }

                    if (isEar)
                    {
                        triangles.Add(new Triangle2D(prev, curr, next));
                        verts.RemoveAt(i);
                        earFound = true;
                        break;
                    }
                }

                if (!earFound) break; // Prevent infinite loop
            }

            if (verts.Count == 3)
            {
                triangles.Add(new Triangle2D(verts[0], verts[1], verts[2]));
            }

            return triangles;
        }

        private static bool IsConvex(Point2D a, Point2D b, Point2D c)
        {
            double cross = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
            return cross > 0;
        }

        private static bool PointInTriangle(Point2D p, Point2D a, Point2D b, Point2D c)
        {
            double d1 = Sign(p, a, b);
            double d2 = Sign(p, b, c);
            double d3 = Sign(p, c, a);

            bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
            bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);

            return !(hasNeg && hasPos);
        }

        private static double Sign(Point2D p1, Point2D p2, Point2D p3)
        {
            return (p1.X - p3.X) * (p2.Y - p3.Y) - (p2.X - p3.X) * (p1.Y - p3.Y);
        }

        private static double SignedArea(List<Point2D> poly)
        {
            double area = 0;
            for (int i = 0; i < poly.Count; i++)
            {
                Point2D p1 = poly[i];
                Point2D p2 = poly[(i + 1) % poly.Count];
                area += (p1.X * p2.Y - p2.X * p1.Y);
            }
            return area / 2.0;
        }
    }

}
