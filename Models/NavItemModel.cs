using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PlustekBCR.Models
{
    public partial class NavItemModel : ObservableObject
    {
        private string _title = string.Empty;
        public string Title { get => _title; set => SetProperty(ref _title, value); }

        private string _iconSource = string.Empty;
        public string IconSource { get => _iconSource; set => SetProperty(ref _iconSource, value); }

        private bool _hasChildren;
        public bool HasChildren { get => _hasChildren; set => SetProperty(ref _hasChildren, value); }

        private bool _isSelected;
        public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }

        public ObservableCollection<NavItemModel> Children { get; set; } = new();
    }
}
