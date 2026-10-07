using System;
using Microsoft.UI.Xaml.Data;
using PlustekBCR.Models;

namespace PlustekBCR.Helpers
{
    public class ContactFieldRowConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var market = value is MarketCode marketCode ? marketCode : MarketCode.JP;
            var field = parameter as string ?? string.Empty;

            return market == MarketCode.US
                ? field switch
                {
                    "Telephone" => 0,
                    "Mobile" => 1,
                    "Email" => 2,
                    "Website" => 3,
                    "SidebarWebsite" => 3,
                    "SidebarAddress" => 4,
                    "Fax" => 4,
                    "SidebarFax" => 4,
                    "Address" => 5,
                    _ => 0
                }
                : field switch
                {
                    "Address" => 0,
                    "Telephone" => 1,
                    "Mobile" => 2,
                    "Email" => 3,
                    "Website" => 4,
                    "SidebarAddress" => 0,
                    "SidebarFax" => 4,
                    "SidebarWebsite" => 4,
                    "Fax" => 5,
                    _ => 0
                };
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) =>
            throw new NotImplementedException();
    }
}
