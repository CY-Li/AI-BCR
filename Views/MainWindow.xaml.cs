using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using PlustekBCR.Services;
using PlustekBCR.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using PlustekBCR.Helpers;
using PlustekBCR.Models;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;

namespace PlustekBCR.Views
{
    public sealed partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }
        public ObservableCollection<TagFlowItem> AdvancedTagFlowItems { get; } = new();
        private bool _hasCheckedForUpdates;
        private bool _isUpdateCheckRunning;
        private readonly DispatcherTimer _mockPaperSensorTimer;
        private readonly ITagCatalogService _tagCatalogService;
        private bool _isSyncingFilterUi;
        private readonly ObservableCollection<string> _advancedSelectedTags = new();
        private bool _isInSettingsWorkspace;
        private Type _lastCardsPageType = typeof(AllCardsPage);
        private object? _lastCardsNavigationItem;
        private string _currentSettingsSection = "General";
        private bool _isRestoringWorkspaceSelection;

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

                var skipCheckbox = new CheckBox 
                { 
                    Content = "Don't show this again",
                    Margin = new Thickness(0, 8, 0, 0)
                };
                var panel = CreateScanInstructionPanel(skipCheckbox);
                var dialog = DialogHelper.CreateDialog(
                    root,
                    "Ready to start scanning",
                    panel,
                    primaryButtonText: "Scan",
                    secondaryButtonText: "Cancel");

                EnableOutsideTapDismiss(dialog);

                var result = await dialog.ShowAsync();
                return (result == ContentDialogResult.Primary, skipCheckbox.IsChecked ?? false);
            };

            ViewModel.ShowAiDisabledWarningAsync = async () =>
            {
                var root = this.Content?.XamlRoot;
                if (root == null) return false;

                var dialog = DialogHelper.CreateDialog(
                    root,
                    "AI is turned off",
                    "Business cards will not be recognized automatically. You will need to input data manually",
                    primaryButtonText: "Turn On AI",
                    secondaryButtonText: "Continue Manually");

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
            RootNavigationView.SelectedItem = AllCardsItem;
            ContentFrame.Navigate(typeof(AllCardsPage));
            RebuildTagFilterMenu();
            RebuildAdvancedTagFlowItems();
            ApplyWorkspaceState();

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

            if (_isRestoringWorkspaceSelection)
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
                if (tag.StartsWith("Settings:", StringComparison.Ordinal))
                {
                    EnterSettingsWorkspace(tag["Settings:".Length..]);
                    return;
                }

                switch (tag)
                {
                    case "AllCards":
                        ExitSettingsWorkspaceIfNeeded();
                        ClearCardFilters();
                        NavigateToAllCardsPage();
                        break;
                    case "TagsRoot":
                        ExitSettingsWorkspaceIfNeeded();
                        NavigateToAllCardsPage();
                        break;
                    case "TagAction:Add":
                        ExitSettingsWorkspaceIfNeeded();
                        _ = AddTagFromSidebarAsync();
                        NavigateToAllCardsPage();
                        break;
                    default:
                        if (tag.StartsWith("TagFilter:", StringComparison.Ordinal))
                        {
                            ExitSettingsWorkspaceIfNeeded();
                            var selectedTag = tag["TagFilter:".Length..];
                            ApplyTagSearchShortcutFromSidebar(selectedTag);
                            NavigateToAllCardsPage();
                        }
                        else if (tag.StartsWith("RecentPreset:", StringComparison.Ordinal))
                        {
                            ExitSettingsWorkspaceIfNeeded();
                            var preset = tag["RecentPreset:".Length..];
                            ApplyRecentPresetFromSidebar(preset);
                            NavigateToAllCardsPage();
                        }
                        break;
                }
            }
        }

        private void OnTagCatalogChanged()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                RebuildTagFilterMenu();
                PruneAdvancedSelectedTags();
            });
        }

        private void OnSettingsClicked(object sender, RoutedEventArgs e)
        {
            EnterSettingsWorkspace(_currentSettingsSection);
        }

        public void ReturnToCardsWorkspace()
        {
            ExitSettingsWorkspaceIfNeeded();

            try
            {
                _isRestoringWorkspaceSelection = true;
                SelectAllCardsNavigationItem();
                NavigateToAllCardsPage();
            }
            finally
            {
                _isRestoringWorkspaceSelection = false;
            }
        }

        private void EnterSettingsWorkspace(string section)
        {
            RememberCardsWorkspaceState();

            _isInSettingsWorkspace = true;
            _currentSettingsSection = string.IsNullOrWhiteSpace(section) ? "General" : section;
            ApplyWorkspaceState();
            SelectSettingsNavigationItem(_currentSettingsSection);
            ContentFrame.Navigate(typeof(SettingsPage), _currentSettingsSection);
        }

        private void ExitSettingsWorkspaceIfNeeded()
        {
            if (!_isInSettingsWorkspace)
            {
                return;
            }

            _isInSettingsWorkspace = false;
            ApplyWorkspaceState();
        }

        private void RememberCardsWorkspaceState()
        {
            if (_isInSettingsWorkspace)
            {
                return;
            }

            if (ContentFrame.SourcePageType != null && ContentFrame.SourcePageType != typeof(SettingsPage))
            {
                _lastCardsPageType = ContentFrame.SourcePageType;
            }

            if (RootNavigationView.SelectedItem is NavigationViewItem selectedItem
                && selectedItem.Tag is string tag
                && !tag.StartsWith("Settings:", StringComparison.Ordinal))
            {
                _lastCardsNavigationItem = selectedItem;
            }
        }

        private void ApplyWorkspaceState()
        {
            var cardsVisibility = _isInSettingsWorkspace ? Visibility.Collapsed : Visibility.Visible;
            var settingsVisibility = _isInSettingsWorkspace ? Visibility.Visible : Visibility.Collapsed;
            var headerWorkspaceVisibility = _isInSettingsWorkspace ? Visibility.Collapsed : Visibility.Visible;

            AllCardsItem.Visibility = cardsVisibility;
            CardsNavSeparator.Visibility = cardsVisibility;
            ByDateItem.Visibility = cardsVisibility;
            ByCompanyItem.Visibility = cardsVisibility;
            ByNameItem.Visibility = cardsVisibility;
            TagsItem.Visibility = cardsVisibility;

            SettingsNavSeparator.Visibility = settingsVisibility;
            SettingsGeneralItem.Visibility = settingsVisibility;
            SettingsImportItem.Visibility = settingsVisibility;
            SettingsOcrItem.Visibility = settingsVisibility;
            SettingsScannerItem.Visibility = settingsVisibility;
            SettingsAboutItem.Visibility = settingsVisibility;

            SearchRoot.Visibility = headerWorkspaceVisibility;
            HeaderGridButton.Visibility = headerWorkspaceVisibility;
            HeaderListButton.Visibility = headerWorkspaceVisibility;
            HeaderViewDivider.Visibility = headerWorkspaceVisibility;

            HeaderSettingButton.Background = _isInSettingsWorkspace
                ? (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"]
                : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            HeaderSettingButton.BorderBrush = _isInSettingsWorkspace
                ? (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"]
                : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            HeaderSettingButton.BorderThickness = _isInSettingsWorkspace
                ? new Thickness(1)
                : new Thickness(0);
        }

        private void SelectSettingsNavigationItem(string section)
        {
            var settingsItems = new Dictionary<string, NavigationViewItem>(StringComparer.OrdinalIgnoreCase)
            {
                ["General"] = SettingsGeneralItem,
                ["Import"] = SettingsImportItem,
                ["OcrAi"] = SettingsOcrItem,
                ["Scanner"] = SettingsScannerItem,
                ["About"] = SettingsAboutItem
            };

            if (!settingsItems.TryGetValue(section, out var navigationItem))
            {
                navigationItem = SettingsGeneralItem;
                _currentSettingsSection = "General";
            }

            RootNavigationView.SelectedItem = navigationItem;
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
            NavigateToAllCardsPage();
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
                NavigateToAllCardsPage();
            }
        }

        private void ApplyTagSearchShortcutFromSidebar(string tag)
        {
            RunWithSearchUiSync(() =>
            {
                ViewModel.ApplyTagSearchKeyword(tag);
                ViewModel.SelectedSearchScope = "Tag";
                HeaderSearchBox.Text = tag;
                UpdateSearchInputMode();
                SelectAllCardsNavigationItem();
            });
        }

        private void ApplyRecentPresetFromSidebar(string preset)
        {
            RunWithSearchUiSync(() =>
            {
                ViewModel.ApplyRecentPreset(preset);
                ClearHeaderSearchText();
                SyncHeaderDatePickersFromViewModel();
                UpdateSearchInputMode();
                SelectAllCardsNavigationItem();
            });

            OpenSearchDropdown(showAdvanced: false);
        }

        private void ClearCardFilters()
        {
            RunWithSearchUiSync(() =>
            {
                ViewModel.ClearSearch();
                ClearHeaderSearchText();
                ClearAdvancedSearchFields();
                ClearHeaderDateFields();
                UpdateSearchInputMode();
                SelectAllCardsNavigationItem();
            });
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
            RunWithSearchUiSync(() =>
            {
                SelectAllCardsNavigationItem();
            });

            NavigateToAllCardsPage();
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
                    || string.Equals(ViewModel.TagSearchKeyword, tag, StringComparison.OrdinalIgnoreCase)
                    || TagTextHelper.ContainsIgnoreCase(ViewModel.AdvancedTagSearchKeywords, tag))
                {
                    ClearCardFilters();
                    NavigateToAllCardsPage();
                }
            }
        }

        private static void RemoveTagFromAllCards(string tagToRemove)
        {
            var allCardsViewModel = App.GetService<AllCardsViewModel>();
            foreach (var card in allCardsViewModel.AllCards)
            {
                var tags = TagTextHelper.Split(card.Tag)
                    .Where(x => !string.Equals(x, tagToRemove, StringComparison.OrdinalIgnoreCase));

                card.Tag = TagTextHelper.Join(tags);
            }
        }

        private async Task AddTagFromSidebarAsync()
        {
            var value = await TagDialogHelper.PromptForNewTagAsync(this.Content?.XamlRoot);
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (_tagCatalogService.AddTag(value))
            {
                await _tagCatalogService.SaveAsync();
                NavigateToAllCardsPage();
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
                    _mockPaperSensorTimer.Stop();
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

        private static StackPanel CreateScanInstructionPanel(CheckBox skipCheckbox)
        {
            var panel = new StackPanel { Spacing = 16 };
            panel.Children.Add(new TextBlock
            {
                Text = "Please scan the business card, placing it face up in the scanner.",
                TextWrapping = TextWrapping.WrapWholeWords,
                FontSize = 16
            });
            panel.Children.Add(new Image
            {
                Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///Assets/scanner_illustration.png")),
                MaxHeight = 200,
                Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform,
                Margin = new Thickness(0, 8, 0, 8)
            });
            panel.Children.Add(skipCheckbox);
            return panel;
        }

        private void EnableOutsideTapDismiss(ContentDialog dialog)
        {
            UIElement? popupRoot = null;
            Microsoft.UI.Xaml.Input.PointerEventHandler? pointerHandler = null;

            pointerHandler = new Microsoft.UI.Xaml.Input.PointerEventHandler((sender, args) =>
            {
                var backgroundElement = FindVisualChildByName(dialog, "BackgroundElement");
                if (backgroundElement == null)
                {
                    return;
                }

                var pos = args.GetCurrentPoint(backgroundElement).Position;
                if (pos.X < 0 || pos.Y < 0 || pos.X > backgroundElement.ActualWidth || pos.Y > backgroundElement.ActualHeight)
                {
                    dialog.Hide();
                }
            });

            dialog.Opened += (s, e) =>
            {
                var root = FindTopLevelElement(dialog);
                if (root == null)
                {
                    return;
                }

                popupRoot = root;
                popupRoot.AddHandler(UIElement.PointerPressedEvent, pointerHandler, true);
            };

            dialog.Closed += (s, e) =>
            {
                if (popupRoot != null && pointerHandler != null)
                {
                    popupRoot.RemoveHandler(UIElement.PointerPressedEvent, pointerHandler);
                }
            };
        }

        private static UIElement? FindTopLevelElement(DependencyObject? start)
        {
            DependencyObject? current = start;
            DependencyObject? root = start;
            while (current != null)
            {
                root = current;
                current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current);
            }

            return root as UIElement;
        }

        private bool _isSearchScopeDropDownOpen = false;

        private void OnHeaderSearchBoxGotFocus(object sender, RoutedEventArgs e)
        {
            OpenDefaultSearchDropdown();
        }

        private void OnHeaderSearchBoxLostFocus(object sender, RoutedEventArgs e)
        {
            // Let SearchRoot interactions keep the dropdown alive.
            // Outside clicks are already handled by OnRootPointerPressed.
        }

        private void OnSearchBoxHostPointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            OpenDefaultSearchDropdown();
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
                SyncAdvancedSearchFieldsFromViewModel();
                UpdateSearchInputMode();
            });
        }

        private void OnAdvancedRecentPresetClicked(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string preset)
            {
                var (start, end) = RecentPresetHelper.GetRange(preset);
                AdvancedStartDatePicker.Date = ToDateTimeOffset(start);
                AdvancedEndDatePicker.Date = ToDateTimeOffset(end);
            }
        }

        private void OnHeaderDateSearchClicked(object sender, RoutedEventArgs e)
        {
            ApplyHeaderDateSearch();
        }

        private void OnHeaderDateClearClicked(object sender, RoutedEventArgs e)
        {
            ClearCardFilters();
            NavigateToAllCardsPage();
        }

        private void OnAdvancedSearchClicked(object sender, RoutedEventArgs e)
        {
            ViewModel.ApplyAdvancedSearch(
                AdvancedCompanyTextBox.Text,
                AdvancedNameTextBox.Text,
                _advancedSelectedTags,
                ToDateTime(AdvancedStartDatePicker.Date),
                ToDateTime(AdvancedEndDatePicker.Date));

            RunWithSearchUiSync(() =>
            {
                ClearHeaderSearchText();
                SyncHeaderDatePickersFromViewModel();
                SelectAllCardsNavigationItem();
            });

            NavigateToAllCardsPage();
            CloseSearchDropdown();
        }

        private void OnAdvancedSearchClearClicked(object sender, RoutedEventArgs e)
        {
            ClearCardFilters();
            NavigateToAllCardsPage();
            CloseSearchDropdown();
        }

        private void ApplyHeaderDateSearch()
        {
            var startDate = ToDateTime(HeaderStartDatePicker.Date);
            var endDate = ToDateTime(HeaderEndDatePicker.Date);
            if (!startDate.HasValue && !endDate.HasValue)
            {
                ClearCardFilters();
                return;
            }

            ViewModel.ApplyDateRange(startDate, endDate, null);
            ViewModel.SelectedSearchScope = "Date";

            RunWithSearchUiSync(() =>
            {
                ClearHeaderSearchText();
                SelectAllCardsNavigationItem();
            });

            NavigateToAllCardsPage();
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

        private void ClearHeaderSearchText()
        {
            if (!string.IsNullOrEmpty(HeaderSearchBox.Text))
            {
                HeaderSearchBox.Text = string.Empty;
            }
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
            AdvancedStartDatePicker.Date = null;
            AdvancedEndDatePicker.Date = null;
            _advancedSelectedTags.Clear();
            RebuildAdvancedTagFlowItems();
        }

        private void SyncAdvancedSearchFieldsFromViewModel()
        {
            AdvancedCompanyTextBox.Text = ViewModel.CompanySearchKeyword ?? string.Empty;
            AdvancedNameTextBox.Text = ViewModel.NameSearchKeyword ?? string.Empty;
            AdvancedStartDatePicker.Date = ToDateTimeOffset(ViewModel.StartDate);
            AdvancedEndDatePicker.Date = ToDateTimeOffset(ViewModel.EndDate);

            _advancedSelectedTags.Clear();
            foreach (var tag in ViewModel.AdvancedTagSearchKeywords)
            {
                TagTextHelper.AddIfMissing(_advancedSelectedTags, tag);
            }

            RebuildAdvancedTagFlowItems();
        }

        private void RebuildAdvancedTagFlowItems()
        {
            CardPageUiHelper.RebuildTagFlowItems(AdvancedTagFlowItems, _advancedSelectedTags);
        }

        private void PruneAdvancedSelectedTags()
        {
            var validTags = _tagCatalogService.GetAllTags();
            var removedTags = _advancedSelectedTags
                .Where(selectedTag => !TagTextHelper.ContainsIgnoreCase(validTags, selectedTag))
                .ToList();

            if (removedTags.Count == 0)
            {
                return;
            }

            foreach (var tag in removedTags)
            {
                _advancedSelectedTags.Remove(tag);
            }

            RebuildAdvancedTagFlowItems();
        }

        private void OnRemoveAdvancedTagClicked(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string tag)
            {
                return;
            }

            if (!TagTextHelper.RemoveFirstIgnoreCase(_advancedSelectedTags, tag))
            {
                return;
            }

            RebuildAdvancedTagFlowItems();
        }

        private void OnAddAdvancedTagClicked(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout
            {
                MenuFlyoutPresenterStyle = (Style)Application.Current.Resources["BcrTagMenuFlyoutPresenterStyle"]
            };
            var available = _tagCatalogService.GetAllTags()
                .Where(tag => !TagTextHelper.ContainsIgnoreCase(_advancedSelectedTags, tag))
                .ToList();

            foreach (var tag in available)
            {
                var item = new MenuFlyoutItem { Text = tag, Tag = tag };
                item.Click += (_, __) =>
                {
                    TagTextHelper.AddIfMissing(_advancedSelectedTags, tag);
                    RebuildAdvancedTagFlowItems();
                };
                flyout.Items.Add(item);
            }

            if (available.Count == 0)
            {
                flyout.Items.Add(new MenuFlyoutItem
                {
                    Text = "No available tags",
                    IsEnabled = false
                });
            }

            flyout.ShowAt((FrameworkElement)sender);
        }

        private static DateTime? ToDateTime(DateTimeOffset? value)
        {
            return value?.Date;
        }

        private static DateTimeOffset? ToDateTimeOffset(DateTime? value)
        {
            return value.HasValue ? new DateTimeOffset(value.Value.Date) : null;
        }

        private void RunWithSearchUiSync(Action action)
        {
            try
            {
                _isSyncingFilterUi = true;
                action();
            }
            finally
            {
                _isSyncingFilterUi = false;
            }
        }

        private void SelectAllCardsNavigationItem()
        {
            RootNavigationView.SelectedItem = AllCardsItem;
        }

        private void NavigateToAllCardsPage()
        {
            ContentFrame.Navigate(typeof(AllCardsPage));
        }

        private void OnHeaderSearchBoxTextChanged(object sender, TextChangedEventArgs e)
        {
            SearchPlaceholder.Visibility = string.IsNullOrEmpty(HeaderSearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            if (_isSyncingFilterUi)
            {
                return;
            }

            if (string.Equals(ViewModel.SelectedSearchScope, "Date", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(HeaderSearchBox.Text) && ViewModel.IsSearchActive)
            {
                ClearCardFilters();
                return;
            }

            if (!string.IsNullOrWhiteSpace(HeaderSearchBox.Text))
            {
                OpenDefaultSearchDropdown();
            }
        }

        private void OnHeaderSearchBoxKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                if (string.Equals(ViewModel.SelectedSearchScope, "Date", StringComparison.OrdinalIgnoreCase))
                {
                    ApplyHeaderDateSearch();
                    return;
                }

                ViewModel.AddRecentSearch(HeaderSearchBox.Text);
                ApplyTextSearchFromHeader();
                if (!string.IsNullOrWhiteSpace(HeaderSearchBox.Text))
                {
                    OpenDefaultSearchDropdown();
                }
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

            if (string.Equals(ViewModel.SelectedSearchScope, "Date", StringComparison.OrdinalIgnoreCase)
                && (ViewModel.StartDate.HasValue || ViewModel.EndDate.HasValue))
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

        private void OpenDefaultSearchDropdown()
        {
            OpenSearchDropdown(showAdvanced: false);
        }

        private void OpenSearchDropdown(bool showAdvanced)
        {
            ShowSearchScopeSelector();
            if (string.Equals(ViewModel.SelectedSearchScope, "Date", StringComparison.OrdinalIgnoreCase) && !showAdvanced)
            {
                ShowDateSearchMode();
                return;
            }

            SetSearchDropdownMode(showAdvanced);

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
            SearchScopeComboBox.Visibility = string.Equals(ViewModel.SelectedSearchScope, "Date", StringComparison.OrdinalIgnoreCase)
                ? Visibility.Visible
                : Visibility.Collapsed;
            AdvancedSearchPanel.Visibility = Visibility.Collapsed;
            DefaultSearchPanel.Visibility = Visibility.Visible;
        }

        private void ShowSearchScopeSelector()
        {
            SearchScopeComboBox.Visibility = Visibility.Visible;
        }

        private void ShowDateSearchMode()
        {
            SearchOverlayLayer.Visibility = Visibility.Collapsed;
            AdvancedSearchPanel.Visibility = Visibility.Collapsed;
            DefaultSearchPanel.Visibility = Visibility.Visible;
        }

        private void SetSearchDropdownMode(bool showAdvanced)
        {
            DefaultSearchPanel.Visibility = showAdvanced ? Visibility.Collapsed : Visibility.Visible;
            AdvancedSearchPanel.Visibility = showAdvanced ? Visibility.Visible : Visibility.Collapsed;
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












