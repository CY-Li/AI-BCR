using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using PlustekBCR.Services;
using PlustekBCR.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using PlustekBCR.Models;
using System;
using System.Diagnostics;

namespace PlustekBCR.Views
{
    public sealed partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }
        private bool _hasCheckedForUpdates;
        private bool _isUpdateCheckRunning;
        private readonly DispatcherTimer _mockPaperSensorTimer;
        private readonly ITagCatalogService _tagCatalogService;
        private bool _isSyncingFilterUi;

        public MainWindow()
        {
            // Assign ViewModel BEFORE InitializeComponent for x:Bind to work
            ViewModel = App.GetService<MainViewModel>();
            _tagCatalogService = App.GetService<ITagCatalogService>();

            this.InitializeComponent();
            RootGrid.AddHandler(UIElement.PointerPressedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler(OnRootPointerPressed), true);
            ViewModel.ScanPulseRequested += OnScanPulseRequested;
            _mockPaperSensorTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1450) };
            _mockPaperSensorTimer.Tick += OnMockPaperSensorTick;

            // Set minimum window size using WinUIEx to avoid flickering
            var manager = WinUIEx.WindowManager.Get(this);
            manager.MinWidth = MinWindowWidth;
            manager.MinHeight = MinWindowHeight;
            
            ViewModel.ShowScanConfirmationDialogAsync = async () =>
            {
                var root = this.Content?.XamlRoot;
                if (root == null)
                {
                    Debug.WriteLine("ShowScanConfirmationDialogAsync: XamlRoot is null.");
                    return (false, false);
                }

                // Create programmatic dialog to avoid XAML generation issues
                var skipCheckbox = new CheckBox 
                { 
                    Content = "Don't show this again",
                    Margin = new Thickness(0, 8, 0, 0)
                };
                var panel = new StackPanel { Spacing = 16 };
                panel.Children.Add(new TextBlock 
                { 
                    Text = "Please scan the business card, placing it face up in the scanner.", 
                    TextWrapping = TextWrapping.WrapWholeWords, 
                    FontSize = 16 
                });
                panel.Children.Add(new Image
                {
                    Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new System.Uri("ms-appx:///Assets/scanner_illustration.png")),
                    MaxHeight = 200,
                    Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform,
                    Margin = new Thickness(0, 8, 0, 8)
                });
                panel.Children.Add(skipCheckbox);

                var dialog = new ContentDialog
                {
                    Title = "Ready to start scanning",
                    Content = panel,
                    PrimaryButtonText = "Scan",
                    SecondaryButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = root
                };

                UIElement? popupRoot = null;
                Microsoft.UI.Xaml.Input.PointerEventHandler? pointerHandler = null;
                pointerHandler = new Microsoft.UI.Xaml.Input.PointerEventHandler((sender, args) =>
                {
                    var backgroundElement = FindVisualChildByName(dialog, "BackgroundElement");
                    if (backgroundElement != null)
                    {
                        var pos = args.GetCurrentPoint(backgroundElement).Position;
                        if (pos.X < 0 || pos.Y < 0 || pos.X > backgroundElement.ActualWidth || pos.Y > backgroundElement.ActualHeight)
                        {
                            dialog.Hide();
                        }
                    }
                });

                dialog.Opened += (s, e) =>
                {
                    DependencyObject current = dialog;
                    DependencyObject root = dialog;
                    while (current != null)
                    {
                        root = current;
                        current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current);
                    }
                    if (root is UIElement uiRoot)
                    {
                        popupRoot = uiRoot;
                        popupRoot.AddHandler(UIElement.PointerPressedEvent, pointerHandler, true);
                    }
                };

                dialog.Closed += (s, e) =>
                {
                    if (popupRoot != null && pointerHandler != null)
                    {
                        popupRoot.RemoveHandler(UIElement.PointerPressedEvent, pointerHandler);
                    }
                };

                var result = await dialog.ShowAsync();
                return (result == ContentDialogResult.Primary, skipCheckbox.IsChecked ?? false);
            };

            ViewModel.ShowAiDisabledWarningAsync = async () =>
            {
                var root = this.Content?.XamlRoot;
                if (root == null) return false;

                var dialog = new ContentDialog
                {
                    Title = "AI is turned off",
                    Content = "Business cards will not be recognized automatically. You will need to input data manually",
                    PrimaryButtonText = "Turn On AI",
                    SecondaryButtonText = "Continue Manually",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = root
                };

                var result = await dialog.ShowAsync();
                return result == ContentDialogResult.Primary;
            };

            // WinUIEx can be used here for window customization if needed
            Title = "Plustek AI BCR";

            // Set initial state of AI button visuals without animation
            if (ViewModel.IsAiEnabled)
            {
                AiIconOff.Opacity = 0;
                AiIconOn.Opacity = 1;
                AiOffText.Opacity = 0;
                AiOnText.Opacity = 1;
                AiOnTextTranslate.Y = 0;
            }
            else
            {
                AiIconOff.Opacity = 1;
                AiIconOn.Opacity = 0;
                AiOffText.Opacity = 1;
                AiOnText.Opacity = 0;
                AiOffTextTranslate.Y = 0;
            }
            _isInitialized = true;
            ScannerReadyPulseStoryboard?.Begin();

            // Set default page
            RootNavigationView.SelectedItem = DashboardItem;
            ContentFrame.Navigate(typeof(EmptyPage));
            RebuildTagFilterMenu();

            // Navigate to AllCards when cards are imported or scanned
            WeakReferenceMessenger.Default.Register<CardsImportedMessage>(this, (r, m) =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (RootNavigationView.SelectedItem as NavigationViewItem == AllCardsItem)
                    {
                        // If AllCardsItem is already selected, SelectionChanged won't fire.
                        // Force manual navigation here to go back from sub-pages (like CardDetailPage/Edit page).
                        ContentFrame.Navigate(typeof(AllCardsPage));
                    }
                    else
                    {
                        // This will trigger OnSelectionChanged, which handles the navigation
                        RootNavigationView.SelectedItem = AllCardsItem;
                    }
                });
            });

            Activated += async (_, _) => await EnsureUpdateCheckAsync();
            Closed += OnWindowClosed;
            _tagCatalogService.TagsChanged += OnTagCatalogChanged;
            ViewModel.SearchChanged += OnSearchChanged;
            UpdateSearchInputMode();
        }



        private void OnWindowClosed(object sender, WindowEventArgs args)
        {
            ScannerReadyPulseStoryboard?.Stop();
            ViewModel.ScanPulseRequested -= OnScanPulseRequested;
            _mockPaperSensorTimer.Stop();
            _mockPaperSensorTimer.Tick -= OnMockPaperSensorTick;
            _tagCatalogService.TagsChanged -= OnTagCatalogChanged;
            ViewModel.SearchChanged -= OnSearchChanged;
        }
        private async Task EnsureUpdateCheckAsync()
        {
            if (_hasCheckedForUpdates || _isUpdateCheckRunning)
            {
                return;
            }

            _isUpdateCheckRunning = true;
            try
            {
                // XamlRoot may not be ready on first activation. Retry briefly.
                const int maxAttempts = 5;
                for (var i = 0; i < maxAttempts; i++)
                {
                    var xamlRoot = this.Content?.XamlRoot;
                    if (xamlRoot != null)
                    {
                        var updateService = App.GetService<IUpdateService>();
                        await updateService.CheckForUpdatesAsync(xamlRoot);
                        _hasCheckedForUpdates = true;
                        return;
                    }

                    await Task.Delay(200);
                }
            }
            finally
            {
                _isUpdateCheckRunning = false;
            }
        }

        private bool _isInitialized = false;

        private void OnAiChecked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (!_isInitialized) return;
            if (ActivateAiStoryboard != null)
            {
                DeactivateAiStoryboard?.Stop();
                ActivateAiStoryboard.Begin();
            }
        }

        private void OnAiUnchecked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (!_isInitialized) return;
            if (DeactivateAiStoryboard != null)
            {
                ActivateAiStoryboard?.Stop();
                DeactivateAiStoryboard.Begin();
            }
        }

        private void OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (_isSyncingFilterUi)
            {
                return;
            }

            if (args.IsSettingsSelected)
            {
                // Navigate to settings if implemented
                return;
            }

            var selectedItem = args.SelectedItemContainer as NavigationViewItem;
            if (selectedItem?.Tag is string tag)
            {
                switch (tag)
                {
                    case "Dashboard":
                        ContentFrame.Navigate(typeof(EmptyPage));
                        break;
                    case "AllCards":
                        ClearCardFilters();
                        ContentFrame.Navigate(typeof(AllCardsPage));
                        break;
                    case "TagsRoot":
                        ContentFrame.Navigate(typeof(AllCardsPage));
                        break;
                    case "TagAction:Add":
                        _ = AddTagFromSidebarAsync();
                        ContentFrame.Navigate(typeof(AllCardsPage));
                        break;
                    default:
                        if (tag.StartsWith("TagFilter:", StringComparison.Ordinal))
                        {
                            var selectedTag = tag["TagFilter:".Length..];
                            ApplyTagSearchShortcutFromSidebar(selectedTag);
                            ContentFrame.Navigate(typeof(AllCardsPage));
                        }
                        else if (tag.StartsWith("RecentPreset:", StringComparison.Ordinal))
                        {
                            var preset = tag["RecentPreset:".Length..];
                            ApplyRecentPresetFromSidebar(preset);
                            ContentFrame.Navigate(typeof(AllCardsPage));
                        }
                        break;
                }
            }
        }

        private void OnTagCatalogChanged()
        {
            DispatcherQueue.TryEnqueue(RebuildTagFilterMenu);
        }

        private void RebuildTagFilterMenu()
        {
            TagsItem.MenuItems.Clear();
            var addTagItem = new NavigationViewItem
            {
                Content = "+ Add tag",
                Tag = "TagAction:Add",
                SelectsOnInvoked = false
            };
            addTagItem.Tapped += OnSidebarAddTagTapped;
            TagsItem.MenuItems.Add(addTagItem);

            foreach (var tag in _tagCatalogService.GetAllTags())
            {
                var item = new NavigationViewItem
                {
                    Tag = $"TagFilter:{tag}",
                    SelectsOnInvoked = false
                };
                item.Tapped += OnSidebarTagTapped;

                var contentGrid = new Grid();
                contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var textBlock = new TextBlock
                {
                    Text = tag,
                    VerticalAlignment = VerticalAlignment.Center
                };

                                var deleteButton = new Button
                {
                    Tag = tag,
                    Width = 24,
                    Height = 24,
                    Margin = new Thickness(8, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Padding = new Thickness(0),
                    Content = new FontIcon
                    {
                        Glyph = "\uE74D",
                        FontSize = 12,
                        Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray)
                    }
                };
                deleteButton.Click += OnSidebarTagDeleteClicked;
                deleteButton.Tapped += (_, args) => args.Handled = true;

                contentGrid.Children.Add(textBlock);
                Grid.SetColumn(deleteButton, 1);
                contentGrid.Children.Add(deleteButton);

                item.Content = contentGrid;
                TagsItem.MenuItems.Add(item);
            }
        }

        private void OnSidebarAddTagTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            e.Handled = true;
            _ = AddTagFromSidebarAsync();
            ContentFrame.Navigate(typeof(AllCardsPage));
        }

        private void OnSidebarTagTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            if (sender is not NavigationViewItem item || item.Tag is not string rawTag)
            {
                return;
            }

            e.Handled = true;
            if (rawTag.StartsWith("TagFilter:", StringComparison.Ordinal))
            {
                var selectedTag = rawTag["TagFilter:".Length..];
                ApplyTagSearchShortcutFromSidebar(selectedTag);
                ContentFrame.Navigate(typeof(AllCardsPage));
            }
        }

        private void ApplyTagSearchShortcutFromSidebar(string tag)
        {
            try
            {
                _isSyncingFilterUi = true;
                ViewModel.ApplyTagSearchKeyword(tag);
                ViewModel.SelectedSearchScope = "Tag";
                HeaderSearchBox.Text = tag;
                UpdateSearchInputMode();
                RootNavigationView.SelectedItem = AllCardsItem;
            }
            finally
            {
                _isSyncingFilterUi = false;
            }
        }

        private void ApplyRecentPresetFromSidebar(string preset)
        {
            try
            {
                _isSyncingFilterUi = true;
                ViewModel.ApplyRecentPreset(preset);
                HeaderSearchBox.Text = string.Empty;
                SyncHeaderDatePickersFromViewModel();
                UpdateSearchInputMode();
                RootNavigationView.SelectedItem = AllCardsItem;
            }
            finally
            {
                _isSyncingFilterUi = false;
            }
        }

        private void ClearCardFilters()
        {
            try
            {
                _isSyncingFilterUi = true;
                ViewModel.ClearSearch();
                if (!string.IsNullOrEmpty(HeaderSearchBox.Text))
                {
                    HeaderSearchBox.Text = string.Empty;
                }
                ClearAdvancedSearchFields();
                ClearHeaderDateFields();
                UpdateSearchInputMode();
                RootNavigationView.SelectedItem = AllCardsItem;
            }
            finally
            {
                _isSyncingFilterUi = false;
            }
        }

        private void ApplyTextSearchFromHeader()
        {
            var keyword = HeaderSearchBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(keyword))
            {
                ClearCardFilters();
                return;
            }

            ViewModel.ApplyHeaderSearch(ViewModel.SelectedSearchScope, keyword);
            try
            {
                _isSyncingFilterUi = true;
                RootNavigationView.SelectedItem = AllCardsItem;
            }
            finally
            {
                _isSyncingFilterUi = false;
            }
            ContentFrame.Navigate(typeof(AllCardsPage));
        }

        private async void OnSidebarTagDeleteClicked(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string tag)
            {
                return;
            }

            if (_tagCatalogService.RemoveTag(tag))
            {
                RemoveTagFromAllCards(tag);
                await _tagCatalogService.SaveAsync();
                if (string.Equals(ViewModel.SelectedTagFilter, tag, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(ViewModel.TagSearchKeyword, tag, StringComparison.OrdinalIgnoreCase))
                {
                    ClearCardFilters();
                    ContentFrame.Navigate(typeof(AllCardsPage));
                }
            }
        }

        private static void RemoveTagFromAllCards(string tagToRemove)
        {
            var allCardsViewModel = App.GetService<AllCardsViewModel>();
            foreach (var card in allCardsViewModel.AllCards)
            {
                var tags = (card.Tag ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Where(x => !string.Equals(x, tagToRemove, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                card.Tag = string.Join(", ", tags);
            }
        }

        private async Task AddTagFromSidebarAsync()
        {
            var input = new TextBox
            {
                PlaceholderText = "Enter a new tag"
            };

            var dialog = new ContentDialog
            {
                Title = "Add tag",
                Content = input,
                PrimaryButtonText = "Add",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content?.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            var value = input.Text?.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (_tagCatalogService.AddTag(value))
            {
                await _tagCatalogService.SaveAsync();
                ContentFrame.Navigate(typeof(AllCardsPage));
            }
        }

        private bool _isDialogShowing = false;
        private async void OnScanClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (_isDialogShowing) return;

            try 
            {
                _isDialogShowing = true;
                if (ViewModel.ScanCommand.CanExecute(null))
                {
                    await ViewModel.ScanCommand.ExecuteAsync(null);
                    if (ViewModel.IsAutoScanMode)
                    {
                        _mockPaperSensorTimer.Stop();
                    }
                    else
                    {
                        _mockPaperSensorTimer.Stop();
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"OnScanButtonClicked error ({ex.GetType().Name}): {ex.Message}");
            }
            finally
            {
                _isDialogShowing = false;
            }
        }

        private async void OnMockPaperSensorTick(object? sender, object e)
        {
            await Task.CompletedTask;
        }

        private void OnScanPulseRequested()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    AutoScanPulseStoryboard?.Stop();
                    AutoScanPulseStoryboard?.Begin();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"OnScanPulseRequested error ({ex.GetType().Name}): {ex.Message}");
                }
            });
        }
        private void OnGlobalDragOver(object sender, Microsoft.UI.Xaml.DragEventArgs e)
        {
            if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
            {
                e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
                e.Handled = true;
            }
            else
            {
                e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.None;
            }
        }

        private async void OnImportClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            try
            {
                var dialog = new ImportDialog
                {
                    XamlRoot = this.Content.XamlRoot
                };
                await dialog.ShowAsync();
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"OnImportClicked error ({ex.GetType().Name}): {ex.Message}");
            }
        }

        private FrameworkElement? FindVisualChildByName(DependencyObject? parent, string name)
        {
            if (parent == null) return null;
            int count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is FrameworkElement fe && fe.Name == name) return fe;
                var result = FindVisualChildByName(child, name);
                if (result != null) return result;
            }
            return null;
        }

        private bool _isSearchScopeDropDownOpen = false;

        private void OnHeaderSearchBoxGotFocus(object sender, RoutedEventArgs e)
        {
            OpenSearchDropdown(showAdvanced: false);
        }

        private void OnSearchBoxHostPointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            OpenSearchDropdown(showAdvanced: false);
        }

        private void OnSearchScopeDropDownOpened(object sender, object e)
        {
            _isSearchScopeDropDownOpen = true;
        }

        private void OnSearchScopeDropDownClosed(object sender, object e)
        {
            _isSearchScopeDropDownOpen = false;
        }

        private void OnSearchScopeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateSearchInputMode();
        }

        private void OnSearchChanged()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (_isSyncingFilterUi)
                {
                    return;
                }

                SyncHeaderDatePickersFromViewModel();
                UpdateSearchInputMode();
            });
        }

        private void OnRecentPresetClicked(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string preset)
            {
                ApplyRecentPresetFromSidebar(preset);
                ContentFrame.Navigate(typeof(AllCardsPage));
                CloseSearchDropdown();
            }
        }

        private void OnAdvancedRecentPresetClicked(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string preset)
            {
                var (start, end) = GetRecentPresetRange(preset);
                AdvancedStartDatePicker.Date = ToDateTimeOffset(start);
                AdvancedEndDatePicker.Date = ToDateTimeOffset(end);
            }
        }

        private void OnHeaderDateSearchClicked(object sender, RoutedEventArgs e)
        {
            ApplyHeaderDateSearch();
            CloseSearchDropdown();
        }

        private void OnAdvancedSearchClicked(object sender, RoutedEventArgs e)
        {
            ViewModel.ApplyAdvancedSearch(
                AdvancedCompanyTextBox.Text,
                AdvancedNameTextBox.Text,
                AdvancedTagTextBox.Text,
                ToDateTime(AdvancedStartDatePicker.Date),
                ToDateTime(AdvancedEndDatePicker.Date));

            try
            {
                _isSyncingFilterUi = true;
                HeaderSearchBox.Text = string.Empty;
                SyncHeaderDatePickersFromViewModel();
                RootNavigationView.SelectedItem = AllCardsItem;
            }
            finally
            {
                _isSyncingFilterUi = false;
            }

            ContentFrame.Navigate(typeof(AllCardsPage));
            CloseSearchDropdown();
        }

        private void OnAdvancedSearchClearClicked(object sender, RoutedEventArgs e)
        {
            ClearCardFilters();
            ContentFrame.Navigate(typeof(AllCardsPage));
            CloseSearchDropdown();
        }

        private void ApplyHeaderDateSearch()
        {
            ViewModel.ApplyDateRange(
                ToDateTime(HeaderStartDatePicker.Date),
                ToDateTime(HeaderEndDatePicker.Date),
                null);
            ViewModel.SelectedSearchScope = "Date";

            try
            {
                _isSyncingFilterUi = true;
                HeaderSearchBox.Text = string.Empty;
                RootNavigationView.SelectedItem = AllCardsItem;
            }
            finally
            {
                _isSyncingFilterUi = false;
            }

            ContentFrame.Navigate(typeof(AllCardsPage));
        }

        private void UpdateSearchInputMode()
        {
            var isDate = string.Equals(ViewModel.SelectedSearchScope, "Date", StringComparison.OrdinalIgnoreCase);
            SearchBoxHost.Visibility = isDate ? Visibility.Collapsed : Visibility.Visible;
            DateSearchBoxHost.Visibility = isDate ? Visibility.Visible : Visibility.Collapsed;
            SearchPlaceholder.Visibility = !isDate && string.IsNullOrEmpty(HeaderSearchBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void SyncHeaderDatePickersFromViewModel()
        {
            HeaderStartDatePicker.Date = ToDateTimeOffset(ViewModel.StartDate);
            HeaderEndDatePicker.Date = ToDateTimeOffset(ViewModel.EndDate);
        }

        private void ClearHeaderDateFields()
        {
            HeaderStartDatePicker.Date = null;
            HeaderEndDatePicker.Date = null;
        }

        private void ClearAdvancedSearchFields()
        {
            AdvancedCompanyTextBox.Text = string.Empty;
            AdvancedNameTextBox.Text = string.Empty;
            AdvancedTagTextBox.Text = string.Empty;
            AdvancedStartDatePicker.Date = null;
            AdvancedEndDatePicker.Date = null;
        }

        private static (DateTime start, DateTime end) GetRecentPresetRange(string preset)
        {
            var today = DateTime.Today;
            var start = preset switch
            {
                "Today" => today,
                "Within 3 days" => today.AddDays(-2),
                "Within 7 days" => today.AddDays(-6),
                _ => today
            };

            return (start, today);
        }

        private static DateTime? ToDateTime(DateTimeOffset? value)
        {
            return value?.Date;
        }

        private static DateTimeOffset? ToDateTimeOffset(DateTime? value)
        {
            return value.HasValue ? new DateTimeOffset(value.Value.Date) : null;
        }

        private void OnHeaderSearchBoxTextChanged(object sender, TextChangedEventArgs e)
        {
            SearchPlaceholder.Visibility = string.IsNullOrEmpty(HeaderSearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            if (_isSyncingFilterUi)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(HeaderSearchBox.Text)
                && ViewModel.IsSearchActive
                && !string.Equals(ViewModel.SelectedSearchScope, "Date", StringComparison.OrdinalIgnoreCase))
            {
                ClearCardFilters();
            }
        }

        private void OnHeaderSearchBoxKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                if (string.Equals(ViewModel.SelectedSearchScope, "Date", StringComparison.OrdinalIgnoreCase))
                {
                    ApplyHeaderDateSearch();
                    CloseSearchDropdown();
                    return;
                }

                ViewModel.AddRecentSearch(HeaderSearchBox.Text);
                ApplyTextSearchFromHeader();
                CloseSearchDropdown();
            }
            else if (e.Key == Windows.System.VirtualKey.Escape)
            {
                CloseSearchDropdown();
            }
        }

        private void OnRecentSearchItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is string keyword)
            {
                HeaderSearchBox.Text = keyword;
                ViewModel.AddRecentSearch(keyword);
                ApplyTextSearchFromHeader();
                CloseSearchDropdown();
            }
        }

        private void OnOpenAdvancedSearchClicked(object sender, RoutedEventArgs e)
        {
            OpenSearchDropdown(showAdvanced: true);
        }


        private void OnRootPointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (SearchOverlayLayer.Visibility != Visibility.Visible)
            {
                return;
            }

            if (_isSearchScopeDropDownOpen)
            {
                return;
            }

            if (IsPointerInsideRootBounds(e, SearchRoot) || IsPointerInsideRootBounds(e, SearchDropdownPanel))
            {
                return;
            }

            CloseSearchDropdown();
        }


        private void OnSearchDropdownPanelPointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            e.Handled = true;
        }

        private void OpenSearchDropdown(bool showAdvanced)
        {
            SearchScopeComboBox.Visibility = Visibility.Visible;
            DefaultSearchPanel.Visibility = showAdvanced ? Visibility.Collapsed : Visibility.Visible;
            AdvancedSearchPanel.Visibility = showAdvanced ? Visibility.Visible : Visibility.Collapsed;

            SearchRoot.UpdateLayout();

            var origin = SearchInputAnchor.TransformToVisual(SearchOverlayLayer).TransformPoint(new Windows.Foundation.Point(0, 0));
            SearchDropdownPanel.Width = SearchInputAnchor.ActualWidth;
            SearchDropdownPanel.Margin = new Thickness(origin.X, origin.Y + SearchInputAnchor.ActualHeight + 6, 0, 0);
            SearchOverlayLayer.Visibility = Visibility.Visible;
        }

        private void CloseSearchDropdown()
        {
            ResetSearchDropdownState();
        }

        private void ResetSearchDropdownState()
        {
            SearchOverlayLayer.Visibility = Visibility.Collapsed;
            SearchScopeComboBox.Visibility = Visibility.Collapsed;
            AdvancedSearchPanel.Visibility = Visibility.Collapsed;
            DefaultSearchPanel.Visibility = Visibility.Visible;
        }

        private bool IsPointerInsideRootBounds(Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e, FrameworkElement element)
        {
            if (element.Visibility != Visibility.Visible || element.ActualWidth <= 0 || element.ActualHeight <= 0)
            {
                return false;
            }

            try
            {
                var pointer = e.GetCurrentPoint(RootGrid).Position;
                var origin = element.TransformToVisual(RootGrid).TransformPoint(new Windows.Foundation.Point(0, 0));
                return pointer.X >= origin.X
                    && pointer.Y >= origin.Y
                    && pointer.X <= origin.X + element.ActualWidth
                    && pointer.Y <= origin.Y + element.ActualHeight;
            }
            catch
            {
                return false;
            }
        }

        // Minimum window size (in pixels)
        private const int MinWindowWidth = 1440;
        private const int MinWindowHeight = 800;
    }
}












