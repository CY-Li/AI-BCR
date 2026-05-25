using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace PlustekBCR.Helpers
{
    public class StringToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; } = false;

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool isNullOrEmpty = string.IsNullOrWhiteSpace(value as string);
            bool isVisible = Invert ? isNullOrEmpty : !isNullOrEmpty;
            return isVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
