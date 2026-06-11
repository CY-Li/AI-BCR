using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using PlustekBCR.Models;
using PlustekBCR.Services;

namespace PlustekBCR.Helpers
{
    public class FieldVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var token = parameter as string;
            if (string.IsNullOrWhiteSpace(token))
            {
                return Visibility.Visible;
            }

            var segments = token.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (segments.Length != 2)
            {
                return Visibility.Visible;
            }

            var surface = segments[0].ToLowerInvariant() switch
            {
                "detail" => BusinessCardSurface.Detail,
                "import" => BusinessCardSurface.Import,
                "export" => BusinessCardSurface.Export,
                _ => BusinessCardSurface.Edit
            };

            var fieldService = App.GetService<IBusinessCardFieldService>();
            return fieldService.IsVisible(segments[1], surface) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }
}
