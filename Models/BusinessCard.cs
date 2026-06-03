using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PlustekBCR.Models
{
    public enum ProcessingStatus
    {
        Pending,       // 待處理
        Recognizing,   // AI 辨認中...
        Done,          // 由AI辨識
        Manual    // 手動輸入（AI OFF 時）
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
        public partial DateTime ScanDate { get; set; }

        [ObservableProperty]
        public partial string Name { get; set; }

        [ObservableProperty]
        public partial string Title { get; set; }

        [ObservableProperty]
        public partial string Company { get; set; }

        [ObservableProperty]
        public partial string Phone { get; set; }

        [ObservableProperty]
        public partial string Email { get; set; }

        [ObservableProperty]
        public partial string Address { get; set; }

        [ObservableProperty]
        public partial string Country { get; set; }

        [ObservableProperty]
        public partial string Tag { get; set; }

        [ObservableProperty]
        public partial string Website { get; set; }

        [ObservableProperty]
        public partial List<Note> Notes { get; set; }

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

        partial void OnStatusChanged(ProcessingStatus value)
        {
            OnPropertyChanged(nameof(IsRecognizing));
            OnPropertyChanged(nameof(IsAiReprocessAvailable));
        }

        public BusinessCard()
        {
            Id = Guid.NewGuid();
            ScanDate = DateTime.Now;
            Name = string.Empty;
            Title = string.Empty;
            Company = string.Empty;
            Phone = string.Empty;
            Email = string.Empty;
            Address = string.Empty;
            Country = string.Empty;
            Tag = string.Empty;
            Website = string.Empty;
            Notes = new();
            Status = ProcessingStatus.Pending;
            IsAutoScanSession = false;
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
