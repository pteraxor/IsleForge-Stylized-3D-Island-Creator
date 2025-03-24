using System.Windows;
using System.Windows.Media.Imaging;

namespace Prototyping.Tools
{
    public interface IDrawingTool
    {
        void OnMouseDown(Point position, WriteableBitmap bitmap);
        void OnMouseMove(Point position, WriteableBitmap bitmap);
        void OnMouseUp(Point position, WriteableBitmap bitmap);
    }
}
