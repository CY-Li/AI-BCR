using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using PlustekBCR.Helpers;

namespace PlustekBCR.Models
{
    public enum ProcessingStatus
    {
        Pending,
        Recognizing,
        Done,
        Manual
    }

    public enum MarketCode
    {
        JP,
        US
    }

    public class Note
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string Content { get; set; } = string.Empty;
    }

    public partial class BusinessCard : ObservableObject
    {
        [ObservableProperty]
        public partial Guid Id { get; set; }

        [ObservableProperty]
        public partial MarketCode MarketCode { get; set; }

        [ObservableProperty]
        public partial DateTime ScanDate { get; set; }

        [ObservableProperty]
        public partial string CompanyName { get; set; }

        [ObservableProperty]
        public partial string Department1 { get; set; }

        [ObservableProperty]
        public partial string Department2 { get; set; }

        [ObservableProperty]
        public partial string Department3 { get; set; }

        [ObservableProperty]
        public partial string Department4 { get; set; }

        [ObservableProperty]
        public partial string DepartmentFull { get; set; }

        [ObservableProperty]
        public partial string JobTitle { get; set; }

        [ObservableProperty]
        public partial string LastName { get; set; }

        [ObservableProperty]
        public partial string MiddleName { get; set; }

        [ObservableProperty]
        public partial string FirstName { get; set; }

        [ObservableProperty]
        public partial string Suffix { get; set; }

        [ObservableProperty]
        public partial string FullName { get; set; }

        [ObservableProperty]
        public partial string LastNameKana { get; set; }

        [ObservableProperty]
        public partial string FirstNameKana { get; set; }

        [ObservableProperty]
        public partial string FullNameKana { get; set; }

        [ObservableProperty]
        public partial string ZipCode { get; set; }

        [ObservableProperty]
        public partial string Country { get; set; }

        [ObservableProperty]
        public partial string State { get; set; }

        [ObservableProperty]
        public partial string City { get; set; }

        [ObservableProperty]
        public partial string AddressLine1 { get; set; }

        [ObservableProperty]
        public partial string AddressLine2 { get; set; }

        [ObservableProperty]
        public partial string FullAddress { get; set; }

        [ObservableProperty]
        public partial string Tel { get; set; }

        [ObservableProperty]
        public partial string Extension { get; set; }

        [ObservableProperty]
        public partial string Fax { get; set; }

        [ObservableProperty]
        public partial string Mobile { get; set; }

        [ObservableProperty]
        public partial string Email { get; set; }

        [ObservableProperty]
        public partial string Website { get; set; }

        [ObservableProperty]
        public partial List<Note> Notes { get; set; }

        [ObservableProperty]
        public partial string Tag { get; set; }

        [ObservableProperty]
        public partial byte[]? FrontImageData { get; set; }

        [ObservableProperty]
        public partial byte[]? BackImageData { get; set; }

        [ObservableProperty]
        public partial ProcessingStatus Status { get; set; }

        [ObservableProperty]
        public partial bool IsAutoScanSession { get; set; }

        public bool IsRecognizing => Status == ProcessingStatus.Recognizing;
        public bool IsAiReprocessAvailable => !IsRecognizing;
        public string DisplayName => !string.IsNullOrWhiteSpace(FullName) ? FullName : BusinessCardAddressHelper.ComposeFullName(MarketCode, FirstName, MiddleName, LastName, Suffix);

        partial void OnStatusChanged(ProcessingStatus value)
        {
            OnPropertyChanged(nameof(IsRecognizing));
            OnPropertyChanged(nameof(IsAiReprocessAvailable));
        }

        partial void OnFirstNameChanged(string value) => SyncDerivedIdentityAndAddress();
        partial void OnMiddleNameChanged(string value) => SyncDerivedIdentityAndAddress();
        partial void OnLastNameChanged(string value) => SyncDerivedIdentityAndAddress();
        partial void OnSuffixChanged(string value) => SyncDerivedIdentityAndAddress();
        partial void OnDepartment1Changed(string value) => SyncDerivedIdentityAndAddress();
        partial void OnDepartment2Changed(string value) => SyncDerivedIdentityAndAddress();
        partial void OnDepartment3Changed(string value) => SyncDerivedIdentityAndAddress();
        partial void OnDepartment4Changed(string value) => SyncDerivedIdentityAndAddress();
        partial void OnAddressLine1Changed(string value) => SyncDerivedIdentityAndAddress();
        partial void OnAddressLine2Changed(string value) => SyncDerivedIdentityAndAddress();
        partial void OnCityChanged(string value) => SyncDerivedIdentityAndAddress();
        partial void OnStateChanged(string value) => SyncDerivedIdentityAndAddress();
        partial void OnZipCodeChanged(string value) => SyncDerivedIdentityAndAddress();
        partial void OnCountryChanged(string value) => SyncDerivedIdentityAndAddress();
        partial void OnFirstNameKanaChanged(string value) => SyncKanaIdentity();
        partial void OnLastNameKanaChanged(string value) => SyncKanaIdentity();

        partial void OnFullNameChanged(string value)
        {
            OnPropertyChanged(nameof(DisplayName));
        }

        public BusinessCard()
        {
            Id = Guid.NewGuid();
            MarketCode = MarketCode.JP;
            ScanDate = DateTime.Now;
            CompanyName = string.Empty;
            Department1 = string.Empty;
            Department2 = string.Empty;
            Department3 = string.Empty;
            Department4 = string.Empty;
            DepartmentFull = string.Empty;
            JobTitle = string.Empty;
            LastName = string.Empty;
            MiddleName = string.Empty;
            FirstName = string.Empty;
            Suffix = string.Empty;
            FullName = string.Empty;
            LastNameKana = string.Empty;
            FirstNameKana = string.Empty;
            FullNameKana = string.Empty;
            ZipCode = string.Empty;
            Country = string.Empty;
            State = string.Empty;
            City = string.Empty;
            AddressLine1 = string.Empty;
            AddressLine2 = string.Empty;
            FullAddress = string.Empty;
            Tel = string.Empty;
            Extension = string.Empty;
            Fax = string.Empty;
            Mobile = string.Empty;
            Email = string.Empty;
            Website = string.Empty;
            Notes = new();
            Tag = string.Empty;
            Status = ProcessingStatus.Pending;
            IsAutoScanSession = false;
        }

        public void PopulateDerivedFieldsFromStructuredValues()
        {
            if (string.IsNullOrWhiteSpace(FullName))
            {
                FullName = BusinessCardAddressHelper.ComposeFullName(MarketCode, FirstName, MiddleName, LastName, Suffix);
            }

            if (string.IsNullOrWhiteSpace(DepartmentFull))
            {
                DepartmentFull = BusinessCardAddressHelper.ComposeDepartmentFull(Department1, Department2, Department3, Department4);
            }

            if (string.IsNullOrWhiteSpace(FullNameKana))
            {
                FullNameKana = ComposeFullNameKana(MarketCode, FirstNameKana, LastNameKana);
            }

            if (string.IsNullOrWhiteSpace(FullAddress))
            {
                FullAddress = BusinessCardAddressHelper.ComposeFullAddress(MarketCode, AddressLine1, AddressLine2, City, State, ZipCode, Country);
            }

            OnPropertyChanged(nameof(DisplayName));
        }

        private void SyncDerivedIdentityAndAddress()
        {
            var composedName = BusinessCardAddressHelper.ComposeFullName(MarketCode, FirstName, MiddleName, LastName, Suffix);
            if (string.IsNullOrWhiteSpace(FirstName)
                && string.IsNullOrWhiteSpace(MiddleName)
                && string.IsNullOrWhiteSpace(LastName)
                && string.IsNullOrWhiteSpace(Suffix))
            {
                FullName = string.Empty;
            }
            else if (!string.IsNullOrWhiteSpace(composedName))
            {
                FullName = composedName;
            }

            var composedDepartment = BusinessCardAddressHelper.ComposeDepartmentFull(Department1, Department2, Department3, Department4);
            if (string.IsNullOrWhiteSpace(Department1)
                && string.IsNullOrWhiteSpace(Department2)
                && string.IsNullOrWhiteSpace(Department3)
                && string.IsNullOrWhiteSpace(Department4))
            {
                DepartmentFull = string.Empty;
            }
            else if (!string.IsNullOrWhiteSpace(composedDepartment))
            {
                DepartmentFull = composedDepartment;
            }

            var composedAddress = BusinessCardAddressHelper.ComposeFullAddress(MarketCode, AddressLine1, AddressLine2, City, State, ZipCode, Country);
            if (string.IsNullOrWhiteSpace(AddressLine1)
                && string.IsNullOrWhiteSpace(AddressLine2)
                && string.IsNullOrWhiteSpace(City)
                && string.IsNullOrWhiteSpace(State)
                && string.IsNullOrWhiteSpace(ZipCode)
                && string.IsNullOrWhiteSpace(Country))
            {
                FullAddress = string.Empty;
            }
            else if (!string.IsNullOrWhiteSpace(composedAddress))
            {
                FullAddress = composedAddress;
            }
        }

        private void SyncKanaIdentity()
        {
            var composedNameKana = ComposeFullNameKana(MarketCode, FirstNameKana, LastNameKana);
            if (string.IsNullOrWhiteSpace(FirstNameKana)
                && string.IsNullOrWhiteSpace(LastNameKana))
            {
                FullNameKana = string.Empty;
            }
            else if (!string.IsNullOrWhiteSpace(composedNameKana))
            {
                FullNameKana = composedNameKana;
            }
        }

        private static string ComposeFullNameKana(MarketCode marketCode, string? firstNameKana, string? lastNameKana)
        {
            var first = (firstNameKana ?? string.Empty).Trim();
            var last = (lastNameKana ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(first) && string.IsNullOrWhiteSpace(last))
            {
                return string.Empty;
            }

            return marketCode == MarketCode.JP
                ? string.Join(" ", new[] { last, first }.Where(x => !string.IsNullOrWhiteSpace(x)))
                : string.Join(" ", new[] { first, last }.Where(x => !string.IsNullOrWhiteSpace(x)));
        }
    }

    public class CardsImportedMessage
    {
        public List<BusinessCard> Cards { get; }

        public CardsImportedMessage(List<BusinessCard> cards)
        {
            Cards = cards;
        }
    }
}
