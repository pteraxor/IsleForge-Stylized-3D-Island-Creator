using System;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Prototyping.Tools
{
    public interface IDrawingTool
    {
        Func<Point, bool> Mask { get; set; } //all tools will need to be adherent to a mask

        void OnMouseDown(Point position, WriteableBitmap bitmap);
        void OnMouseMove(Point position, WriteableBitmap bitmap);
        void OnMouseUp(Point position, WriteableBitmap bitmap);
    }

}
