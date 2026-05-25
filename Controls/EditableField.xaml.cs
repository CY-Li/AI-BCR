using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace PlustekBCR.Controls
{
    public sealed partial class EditableField : UserControl
    {
        public EditableField()
        {
            this.InitializeComponent();
        }

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(EditableField), new PropertyMetadata(string.Empty, OnTextChanged));

        public string PlaceholderText
        {
            get => (string)GetValue(PlaceholderTextProperty);
            set => SetValue(PlaceholderTextProperty, value);
        }

        public static readonly DependencyProperty PlaceholderTextProperty =
            DependencyProperty.Register(nameof(PlaceholderText), typeof(string), typeof(EditableField), new PropertyMetadata(string.Empty));

        public Visibility HasText(string text) => string.IsNullOrWhiteSpace(text) ? Visibility.Collapsed : Visibility.Visible;
        public Visibility HasNoText(string text) => string.IsNullOrWhiteSpace(text) ? Visibility.Visible : Visibility.Collapsed;

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // Optional: Handle text changes from external source if needed
            if (d is EditableField field && field.EditBox.Visibility != Visibility.Visible)
            {
                // Update bindings if needed
                field.Bindings.Update();
            }
        }

        private void OnGridPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // Hide both display blocks, show EditBox
            DisplayBlock.Visibility = Visibility.Collapsed;
            PlaceholderBlock.Visibility = Visibility.Collapsed;
            EditBox.Visibility = Visibility.Visible;
            EditBox.Focus(FocusState.Programmatic);
            EditBox.SelectAll();
            e.Handled = true; // Prevent event from bubbling
        }

        private void OnEditBoxLostFocus(object sender, RoutedEventArgs e)
        {
            // Exit edit mode
            ExitEditMode();
        }

        private void OnEditBoxKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                ExitEditMode();
                e.Handled = true;
            }
        }

        private void ExitEditMode()
        {
            EditBox.Visibility = Visibility.Collapsed;
            // Show the correct block based on whether text is empty
            if (string.IsNullOrWhiteSpace(Text))
            {
                DisplayBlock.Visibility = Visibility.Collapsed;
                PlaceholderBlock.Visibility = Visibility.Visible;
            }
            else
            {
                DisplayBlock.Visibility = Visibility.Visible;
                PlaceholderBlock.Visibility = Visibility.Collapsed;
            }
        }
    }
}
