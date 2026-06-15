using System;
using System.Collections.Generic;
using System.Linq;
using PlustekBCR.Models;

namespace PlustekBCR.Services
{
    public class BusinessCardFieldService : IBusinessCardFieldService
    {
        private readonly List<BusinessCardFieldDefinition> _fields;
        private readonly IApplicationSettingsService _settingsService;
        private readonly ILocalizationService _localizationService;

        public MarketCode CurrentMarket => _settingsService.CurrentMarket;

        public BusinessCardFieldService(IApplicationSettingsService settingsService)
        {
            _settingsService = settingsService;
            _localizationService = App.GetService<ILocalizationService>();
            _fields = BuildFields();
        }

        public IReadOnlyList<BusinessCardFieldDefinition> GetFields(BusinessCardSurface surface)
        {
            return _fields.Where(field => Supports(field, surface, CurrentMarket)).ToList();
        }

        public bool IsVisible(string key, BusinessCardSurface surface)
        {
            var definition = _fields.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.Ordinal));
            return definition != null && Supports(definition, surface, CurrentMarket);
        }

        public string GetLabel(string key)
        {
            var definition = _fields.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.Ordinal));
            if (definition == null)
            {
                return key;
            }

            var localized = _localizationService.GetString($"Field.{definition.Key}");
            return string.Equals(localized, $"Field.{definition.Key}", StringComparison.Ordinal)
                ? definition.Label
                : localized;
        }

        public string[] GetCsvHeaders(BusinessCardSurface surface)
        {
            return GetFields(surface).Select(x => x.Key).ToArray();
        }

        private static bool Supports(BusinessCardFieldDefinition definition, BusinessCardSurface surface, MarketCode market)
        {
            return surface switch
            {
                BusinessCardSurface.Edit => definition.EditMarkets.Contains(market),
                BusinessCardSurface.Detail => definition.DetailMarkets.Contains(market),
                BusinessCardSurface.Import => definition.ImportMarkets.Contains(market),
                BusinessCardSurface.Export => definition.ExportMarkets.Contains(market),
                _ => false
            };
        }

        private static List<BusinessCardFieldDefinition> BuildFields()
        {
            static HashSet<MarketCode> Markets(params MarketCode[] markets) => new(markets);

            return new List<BusinessCardFieldDefinition>
            {
                new() { Key = "company_name", Label = "Company", PropertyName = nameof(BusinessCard.CompanyName), EditMarkets = Markets(MarketCode.JP, MarketCode.US), DetailMarkets = Markets(MarketCode.JP, MarketCode.US), ImportMarkets = Markets(MarketCode.JP, MarketCode.US), ExportMarkets = Markets(MarketCode.JP, MarketCode.US) },
                new() { Key = "department_1", Label = "Department 1", PropertyName = nameof(BusinessCard.Department1), EditMarkets = Markets(MarketCode.JP, MarketCode.US), DetailMarkets = Markets(MarketCode.JP, MarketCode.US), ImportMarkets = Markets(MarketCode.JP, MarketCode.US), ExportMarkets = Markets(MarketCode.JP, MarketCode.US) },
                new() { Key = "department_2", Label = "Department 2", PropertyName = nameof(BusinessCard.Department2), EditMarkets = Markets(MarketCode.JP, MarketCode.US), DetailMarkets = Markets(MarketCode.JP, MarketCode.US), ImportMarkets = Markets(MarketCode.JP, MarketCode.US), ExportMarkets = Markets(MarketCode.JP, MarketCode.US) },
                new() { Key = "department_3", Label = "Department 3", PropertyName = nameof(BusinessCard.Department3), EditMarkets = Markets(MarketCode.JP, MarketCode.US), DetailMarkets = Markets(MarketCode.JP, MarketCode.US), ImportMarkets = Markets(MarketCode.JP, MarketCode.US), ExportMarkets = Markets(MarketCode.JP, MarketCode.US) },
                new() { Key = "department_4", Label = "Department 4", PropertyName = nameof(BusinessCard.Department4), EditMarkets = Markets(MarketCode.JP, MarketCode.US), DetailMarkets = Markets(MarketCode.JP, MarketCode.US), ImportMarkets = Markets(MarketCode.JP, MarketCode.US), ExportMarkets = Markets(MarketCode.JP, MarketCode.US) },
                new() { Key = "department_full", Label = "Department", PropertyName = nameof(BusinessCard.DepartmentFull), EditMarkets = Markets(MarketCode.JP, MarketCode.US), DetailMarkets = Markets(MarketCode.JP, MarketCode.US), ImportMarkets = Markets(MarketCode.JP, MarketCode.US), ExportMarkets = Markets(MarketCode.JP, MarketCode.US) },
                new() { Key = "job_title", Label = "Job Title", PropertyName = nameof(BusinessCard.JobTitle), EditMarkets = Markets(MarketCode.JP, MarketCode.US), DetailMarkets = Markets(MarketCode.JP, MarketCode.US), ImportMarkets = Markets(MarketCode.JP, MarketCode.US), ExportMarkets = Markets(MarketCode.JP, MarketCode.US) },
                new() { Key = "last_name", Label = "Last Name", PropertyName = nameof(BusinessCard.LastName), EditMarkets = Markets(MarketCode.JP, MarketCode.US), DetailMarkets = Markets(MarketCode.JP, MarketCode.US), ImportMarkets = Markets(MarketCode.JP, MarketCode.US), ExportMarkets = Markets(MarketCode.JP, MarketCode.US) },
                new() { Key = "last_name_kana", Label = "Last Name Kana", PropertyName = nameof(BusinessCard.LastNameKana), EditMarkets = Markets(MarketCode.JP), DetailMarkets = Markets(MarketCode.JP), ImportMarkets = Markets(MarketCode.JP), ExportMarkets = Markets(MarketCode.JP) },
                new() { Key = "middle_name", Label = "Middle Name", PropertyName = nameof(BusinessCard.MiddleName), EditMarkets = Markets(MarketCode.US), DetailMarkets = Markets(MarketCode.US), ImportMarkets = Markets(MarketCode.US), ExportMarkets = Markets(MarketCode.US) },
                new() { Key = "first_name", Label = "First Name", PropertyName = nameof(BusinessCard.FirstName), EditMarkets = Markets(MarketCode.JP, MarketCode.US), DetailMarkets = Markets(MarketCode.JP, MarketCode.US), ImportMarkets = Markets(MarketCode.JP, MarketCode.US), ExportMarkets = Markets(MarketCode.JP, MarketCode.US) },
                new() { Key = "first_name_kana", Label = "First Name Kana", PropertyName = nameof(BusinessCard.FirstNameKana), EditMarkets = Markets(MarketCode.JP), DetailMarkets = Markets(MarketCode.JP), ImportMarkets = Markets(MarketCode.JP), ExportMarkets = Markets(MarketCode.JP) },
                new() { Key = "suffix", Label = "Suffix", PropertyName = nameof(BusinessCard.Suffix), EditMarkets = Markets(MarketCode.US), DetailMarkets = Markets(MarketCode.US), ImportMarkets = Markets(MarketCode.US), ExportMarkets = Markets(MarketCode.US) },
                new() { Key = "full_name", Label = "Full Name", PropertyName = nameof(BusinessCard.FullName), EditMarkets = Markets(MarketCode.JP, MarketCode.US), DetailMarkets = Markets(MarketCode.JP, MarketCode.US), ImportMarkets = Markets(MarketCode.JP, MarketCode.US), ExportMarkets = Markets(MarketCode.JP, MarketCode.US) },
                new() { Key = "full_name_kana", Label = "Full Name Kana", PropertyName = nameof(BusinessCard.FullNameKana), EditMarkets = Markets(MarketCode.JP), DetailMarkets = Markets(MarketCode.JP), ImportMarkets = Markets(MarketCode.JP), ExportMarkets = Markets(MarketCode.JP) },
                new() { Key = "zip_code", Label = "Zip Code", PropertyName = nameof(BusinessCard.ZipCode), EditMarkets = Markets(MarketCode.JP, MarketCode.US), DetailMarkets = Markets(MarketCode.JP, MarketCode.US), ImportMarkets = Markets(MarketCode.JP, MarketCode.US), ExportMarkets = Markets(MarketCode.JP, MarketCode.US) },
                new() { Key = "country", Label = "Country", PropertyName = nameof(BusinessCard.Country), EditMarkets = Markets(MarketCode.US), DetailMarkets = Markets(MarketCode.US), ImportMarkets = Markets(MarketCode.US), ExportMarkets = Markets(MarketCode.US) },
                new() { Key = "state", Label = "State", PropertyName = nameof(BusinessCard.State), EditMarkets = Markets(MarketCode.US), DetailMarkets = Markets(MarketCode.US), ImportMarkets = Markets(MarketCode.US), ExportMarkets = Markets(MarketCode.US) },
                new() { Key = "city", Label = "City", PropertyName = nameof(BusinessCard.City), EditMarkets = Markets(MarketCode.US), DetailMarkets = Markets(MarketCode.US), ImportMarkets = Markets(MarketCode.US), ExportMarkets = Markets(MarketCode.US) },
                new() { Key = "address_line_1", Label = "Address Line 1", PropertyName = nameof(BusinessCard.AddressLine1), EditMarkets = Markets(MarketCode.JP, MarketCode.US), DetailMarkets = Markets(MarketCode.JP, MarketCode.US), ImportMarkets = Markets(MarketCode.JP, MarketCode.US), ExportMarkets = Markets(MarketCode.JP, MarketCode.US) },
                new() { Key = "address_line_2", Label = "Address Line 2", PropertyName = nameof(BusinessCard.AddressLine2), EditMarkets = Markets(MarketCode.US), DetailMarkets = Markets(MarketCode.US), ImportMarkets = Markets(MarketCode.US), ExportMarkets = Markets(MarketCode.US) },
                new() { Key = "full_address", Label = "Full Address", PropertyName = nameof(BusinessCard.FullAddress), EditMarkets = Markets(MarketCode.JP, MarketCode.US), DetailMarkets = Markets(MarketCode.JP, MarketCode.US), ImportMarkets = Markets(MarketCode.JP, MarketCode.US), ExportMarkets = Markets(MarketCode.JP, MarketCode.US) },
                new() { Key = "tel", Label = "Telephone", PropertyName = nameof(BusinessCard.Tel), EditMarkets = Markets(MarketCode.JP, MarketCode.US), DetailMarkets = Markets(MarketCode.JP, MarketCode.US), ImportMarkets = Markets(MarketCode.JP, MarketCode.US), ExportMarkets = Markets(MarketCode.JP, MarketCode.US) },
                new() { Key = "extension", Label = "Extension", PropertyName = nameof(BusinessCard.Extension), EditMarkets = Markets(MarketCode.US), DetailMarkets = Markets(MarketCode.US), ImportMarkets = Markets(MarketCode.US), ExportMarkets = Markets(MarketCode.US) },
                new() { Key = "fax", Label = "Fax", PropertyName = nameof(BusinessCard.Fax), EditMarkets = Markets(MarketCode.JP, MarketCode.US), DetailMarkets = Markets(MarketCode.US), ImportMarkets = Markets(MarketCode.JP, MarketCode.US), ExportMarkets = Markets(MarketCode.JP, MarketCode.US) },
                new() { Key = "mobile", Label = "Mobile", PropertyName = nameof(BusinessCard.Mobile), EditMarkets = Markets(MarketCode.JP, MarketCode.US), DetailMarkets = Markets(MarketCode.JP, MarketCode.US), ImportMarkets = Markets(MarketCode.JP, MarketCode.US), ExportMarkets = Markets(MarketCode.JP, MarketCode.US) },
                new() { Key = "email", Label = "Email", PropertyName = nameof(BusinessCard.Email), EditMarkets = Markets(MarketCode.JP, MarketCode.US), DetailMarkets = Markets(MarketCode.JP, MarketCode.US), ImportMarkets = Markets(MarketCode.JP, MarketCode.US), ExportMarkets = Markets(MarketCode.JP, MarketCode.US) },
                new() { Key = "website", Label = "Website", PropertyName = nameof(BusinessCard.Website), EditMarkets = Markets(MarketCode.JP, MarketCode.US), DetailMarkets = Markets(MarketCode.JP, MarketCode.US), ImportMarkets = Markets(MarketCode.JP, MarketCode.US), ExportMarkets = Markets(MarketCode.JP, MarketCode.US) },
                new() { Key = "notes", Label = "Notes", PropertyName = nameof(BusinessCard.Notes), EditMarkets = Markets(MarketCode.JP, MarketCode.US), DetailMarkets = Markets(), ImportMarkets = Markets(), ExportMarkets = Markets() }
            };
        }
    }
}
