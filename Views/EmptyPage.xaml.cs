using Microsoft.UI.Xaml.Controls;
using PlustekBCR.ViewModels;

namespace PlustekBCR.Views
{
    public sealed partial class EmptyPage : Page
    {
        public EmptyViewModel ViewModel { get; }

        public EmptyPage()
        {
            this.InitializeComponent();
            ViewModel = App.GetService<EmptyViewModel>();
        }
    }
}
