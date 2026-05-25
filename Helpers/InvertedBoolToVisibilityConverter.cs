using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace PlustekBCR.Helpers
{
    public class InvertedBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isTrue)
            {
                return isTrue ? Visibility.Collapsed : Visibility.Visible;
            }
            // Also handle null check for objects (Visible if null)
            return value == null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is Visibility visibility)
            {
                return visibility != Visibility.Visible;
            }
            return false;
        }
    }
}
