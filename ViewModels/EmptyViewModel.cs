using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PlustekBCR.ViewModels
{
    public partial class EmptyViewModel : ObservableObject
    {
        [RelayCommand]
        private async Task ScanNow()
        {
            // Trigger the same scan flow as the main Scan button
            var mainViewModel = App.GetService<MainViewModel>();
            if (mainViewModel.ScanCommand.CanExecute(null))
            {
                await mainViewModel.ScanCommand.ExecuteAsync(null);
            }
        }
    }
}
