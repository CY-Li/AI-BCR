using WinRT.Interop;

namespace PlustekBCR.Helpers
{
    public static class PickerWindowHelper
    {
        public static void Initialize(object picker)
        {
            var hwnd = WindowNative.GetWindowHandle(App.Window);
            InitializeWithWindow.Initialize(picker, hwnd);
        }
    }
}
