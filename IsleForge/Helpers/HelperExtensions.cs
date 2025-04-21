using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows;

namespace IsleForge.Helpers
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

        public T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                    return typedChild;

                T result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }
            return null;
        }

        public static float GetFloatFromTag(DependencyObject parent, string tag, float fallback)
        {
            var input = FindElementByTag<TextBox>(parent, tag);
            if (input != null && float.TryParse(input.Text, out float value))
                return value;
            return fallback;
        }
    }
}
