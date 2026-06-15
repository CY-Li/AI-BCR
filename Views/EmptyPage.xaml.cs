using Microsoft.UI.Xaml.Controls;
using PlustekBCR.Helpers;
using PlustekBCR.ViewModels;

namespace PlustekBCR.Views
{
    public sealed partial class EmptyPage : Page
    {
        public EmptyViewModel ViewModel { get; }

        public EmptyPage()
        {
            this.InitializeComponent();
            DataContext = App.GetService<LocalizedStrings>();
            ViewModel = App.GetService<EmptyViewModel>();
        }
    }
}
