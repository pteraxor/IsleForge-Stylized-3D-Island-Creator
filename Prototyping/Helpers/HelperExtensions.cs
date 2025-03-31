using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Prototyping.Helpers
{
    public class HelperExtensions
    {
        public static T FindElementByTag<T>(DependencyObject parent, string tag) where T : FrameworkElement
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T t && t.Tag?.ToString() == tag)
                    return t;

                var result = FindElementByTag<T>(child, tag);
                if (result != null)
                    return result;
            }
            return null;
        }


    }
}
