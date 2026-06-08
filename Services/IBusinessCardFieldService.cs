using System.Collections.Generic;
using PlustekBCR.Models;

namespace PlustekBCR.Services
{
    public interface IBusinessCardFieldService
    {
        MarketCode CurrentMarket { get; }
        IReadOnlyList<BusinessCardFieldDefinition> GetFields(BusinessCardSurface surface);
        bool IsVisible(string key, BusinessCardSurface surface);
        string GetLabel(string key);
        string[] GetCsvHeaders(BusinessCardSurface surface);
    }
}
