using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace PlustekBCR.Helpers
{
    public class ByteArrayToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool hasData = value is byte[] bytes && bytes.Length > 0;
            return hasData ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
