#nullable enable
using Microsoft.UI.Xaml.Controls;
using System.Numerics;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media;
using PlustekBCR.Controls;
using PlustekBCR.Helpers;
using PlustekBCR.ViewModels;
using PlustekBCR.Models;
using PlustekBCR.Services;

namespace PlustekBCR.Views
{
    public sealed partial class AllCardsPage : Page
    {
        public AllCardsViewModel ViewModel { get; }
        private readonly ITagCatalogService _tagCatalogService;
        private readonly IApplicationSettingsService _settingsService;
        private readonly ILocalizationService _localizationService;
        private readonly IImageViewerService _imageViewerService;
        public ObservableCollection<string> SidebarSelectedTags { get; } = new();
        public ObservableCollection<TagFlowItem> SidebarTagFlowItems { get; } = new();
        private bool _isSynchronizingExportSelection;
        private bool _isExportSelectionSyncQueued;

        public AllCardsPage()
        {
            ViewModel = App.GetService<AllCardsViewModel>();
            _tagCatalogService = App.GetService<ITagCatalogService>();
            _settingsService = App.GetService<IApplicationSettingsService>();
            _localizationService = App.GetService<ILocalizationService>();
            _imageViewerService = App.GetService<IImageViewerService>();
            this.InitializeComponent();
            DataContext = App.GetService<LocalizedStrings>();
            this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
            ViewModel.PropertyChanged += OnViewModelPropertyChanged;
            _tagCatalogService.TagsChanged += OnTagCatalogChanged;
            _settingsService.CurrentMarketChanged += OnCurrentMarketChanged;
            _localizationService.LanguageChanged += OnLanguageChanged;

            ViewModel.ConfirmDeleteCardAsync = async (card) =>
            {
                var dialog = CardPageUiHelper.CreateDeleteConfirmationDialog(card.FullName, this.XamlRoot);
                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    _imageViewerService.Close(card);
                }
                return result == ContentDialogResult.Primary;
            };

            ViewModel.ConfirmDeleteNoteAsync = async (_) =>
            {
                var dialog = CardPageUiHelper.CreateDeleteNoteConfirmationDialog(this.XamlRoot);
                return await dialog.ShowAsync() == ContentDialogResult.Primary;
            };

            ViewModel.ConfirmReplaceDuplicatesAsync = async (card, duplicateCount) =>
            {
                var dialog = CardPageUiHelper.CreateDuplicateReplaceConfirmationDialog(
                    card.FullName,
                    duplicateCount,
                    this.XamlRoot);
                var result = await dialog.ShowAsync();
                return result == ContentDialogResult.Primary;
            };
        }

        protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            RefreshSidebarTagsFromCard();
        }

        protected override void OnNavigatedFrom(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
        }

        private void OnCurrentMarketChanged(MarketCode market)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                Bindings.Update();
            });
        }

        private void OnCardClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is PlustekBCR.Models.BusinessCard card)
            {
                if (ViewModel.IsBatchExportMode)
                {
                    return;
                }

                ViewModel.SelectCardCommand.Execute(card);
            }
        }

        private void OnClearSearchClicked(object sender, RoutedEventArgs e)
        {
            ViewModel.MainViewModel.ClearSearch();
        }

        private void OnSidebarNewNoteKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key != Windows.System.VirtualKey.Enter)
            {
                return;
            }

            if (sender is TextBox textBox)
            {
                ViewModel.NewNoteContent = textBox.Text;
            }

            if (ViewModel.AddNoteCommand.CanExecute(null))
            {
                ViewModel.AddNoteCommand.Execute(null);
            }

            UpdateSidebarNewNotePlaceholderVisibility(ViewModel.NewNoteContent);
            e.Handled = true;
        }

        private void OnSidebarNewNoteTextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                UpdateSidebarNewNotePlaceholderVisibility(textBox.Text);
            }
        }

        private void UpdateSidebarNewNotePlaceholderVisibility(string? text)
        {
            if (SidebarNewNotePlaceholderText == null)
            {
                return;
            }

            SidebarNewNotePlaceholderText.Visibility = string.IsNullOrWhiteSpace(text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void OnDeleteSidebarNoteClicked(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: Note note }
                && ViewModel.DeleteNoteCommand.CanExecute(note))
            {
                ViewModel.DeleteNoteCommand.Execute(note);
            }
        }

        private void OnContactActionInfoBarClosed(InfoBar sender, InfoBarClosedEventArgs args)
        {
            ViewModel.DismissContactActionMessageCommand.Execute(null);
        }

        private void OnCardDoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if (ViewModel.IsBatchExportMode)
            {
                return;
            }

            Microsoft.UI.Xaml.DependencyObject? visualParent = e.OriginalSource as Microsoft.UI.Xaml.DependencyObject;
            while (visualParent != null)
            {
                if (visualParent is GridViewItem gridItem && gridItem.DataContext is BusinessCard gridCard)
                {
                    ViewModel.SelectedCard = gridCard;
                    break;
                }

                if (visualParent is ListViewItem listItem && listItem.DataContext is BusinessCard listCard)
                {
                    ViewModel.SelectedCard = listCard;
                    break;
                }

                visualParent = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(visualParent);
            }

            if (ViewModel.SelectedCard == null || this.Frame == null)
            {
                return;
            }

            NavigateToDetail(ViewModel.SelectedCard);
        }

        private void OnOverlayClicked(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            // Only close if the sidebar is open and we clicked outside the sidebar AND outside any card
            if (ViewModel.IsSidebarOpen)
            {
                // In WinUI 3, we must use VisualTreeHelper to reliably traverse the visual tree, 
                // especially for elements inside ControlTemplates where .Parent might be null.
                Microsoft.UI.Xaml.DependencyObject? visualParent = e.OriginalSource as Microsoft.UI.Xaml.DependencyObject;
                while (visualParent != null)
                {
                    if (visualParent == Sidebar) return; // Clicked inside sidebar, don't close
                    if (visualParent is GridViewItem || visualParent is ListViewItem || visualParent.GetType().Name == "GridViewItem" || visualParent.GetType().Name == "ListViewItem") return; // Clicked a card, don't close
                    visualParent = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(visualParent);
                }

                ViewModel.CloseSidebarCommand.Execute(null);
            }
        }

        private void Card_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is Grid grid)
            {
                // Visual state changes
                // 1. Keep border simple (no more blue line)
                // We'll just use the shadow and scale for the "glow" effect as requested
                
                // 2. Lift the ZIndex so the card stays on top of neighbors
                if (grid.Parent is ContentPresenter cp && cp.Parent is Grid container)
                {
                    if (container.Parent is GridViewItem gridItem) Canvas.SetZIndex(gridItem, 100);
                    else if (container.Parent is ListViewItem listItem) Canvas.SetZIndex(listItem, 100);
                }

                // 3. Safe animations using UIElement properties
                grid.CenterPoint = new Vector3((float)grid.ActualWidth / 2, (float)grid.ActualHeight / 2, 0);
                grid.Scale = new Vector3(1.02f, 1.02f, 1.0f);
                grid.Translation = new Vector3(0, -4, 64); // Deeper shadow (Z) and subtle lift (Y)
            }
        }

        private void Card_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is Grid grid)
            {
                // Reset ZIndex
                if (grid.Parent is ContentPresenter cp && cp.Parent is Grid container)
                {
                    if (container.Parent is GridViewItem gridItem) Canvas.SetZIndex(gridItem, 0);
                    else if (container.Parent is ListViewItem listItem) Canvas.SetZIndex(listItem, 0);
                }

                // Reset safe animations to default state (keeps base shadow)
                grid.Scale = new Vector3(1.0f, 1.0f, 1.0f);
                grid.Translation = new Vector3(0, 0, 16); 
            }
        }

        private void OnEditInfoClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            NavigateToDetail(ViewModel.SelectedCard);
        }

        private void OnOpenSidebarFrontImageViewerClicked(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            if (ViewModel.SelectedCard?.FrontImageData is { Length: > 0 })
            {
                _imageViewerService.Show(ViewModel.SelectedCard, CardImageSide.Front);
            }
        }

        private void OnOpenSidebarBackImageViewerClicked(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            if (ViewModel.SelectedCard?.BackImageData is { Length: > 0 })
            {
                _imageViewerService.Show(ViewModel.SelectedCard, CardImageSide.Back);
            }
        }

        private void OnSidebarSplitterDragDelta(object sender, Microsoft.UI.Xaml.Controls.Primitives.DragDeltaEventArgs e)
        {
            var newWidth = Sidebar.Width - e.HorizontalChange;
            if (newWidth < 280) newWidth = 280;
            if (newWidth > 1200) newWidth = 1200;
            Sidebar.Width = newWidth;
            UpdateCardImagesLayout(newWidth);
        }

        // Threshold (px) above which both front and back images are shown side-by-side
        private const double WideModeSidebarThreshold = 520;

        private void OnSidebarSizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e)
        {
            UpdateCardImagesLayout(e.NewSize.Width);
        }

        private void UpdateCardImagesLayout(double sidebarWidth)
        {
            if (BackImageColumn == null) return;
            if (sidebarWidth >= WideModeSidebarThreshold)
            {
                // Wide mode: give back image an equal share
                BackImageColumn.Width = new Microsoft.UI.Xaml.GridLength(1, Microsoft.UI.Xaml.GridUnitType.Star);
            }
            else
            {
                // Narrow mode: collapse the back image column
                BackImageColumn.Width = new Microsoft.UI.Xaml.GridLength(0);
            }
        }

        private void SidebarSplitter_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            this.ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.SizeWestEast);
        }

        private void SidebarSplitter_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            this.ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Arrow);
        }

        private async void OnDeleteContextClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is PlustekBCR.Models.BusinessCard card)
            {
                if (ViewModel.DeleteCardCommand.CanExecute(card))
                {
                    await ViewModel.DeleteCardCommand.ExecuteAsync(card);
                }
            }
        }

        private void OnViewDetailsContextClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is PlustekBCR.Models.BusinessCard card)
            {
                ViewModel.SelectedCard = card;
                NavigateToDetail(card);
            }
        }

        private void OnCardContextFlyoutOpening(object sender, object e)
        {
            if (sender is not MenuFlyout flyout || flyout.Items.Count < 3)
            {
                return;
            }

            if (flyout.Items[0] is MenuFlyoutItem viewDetailsItem)
            {
                viewDetailsItem.Text = _localizationService.GetString("Button.ViewDetails");
            }

            if (flyout.Items[1] is MenuFlyoutSubItem exportSubItem)
            {
                exportSubItem.Text = _localizationService.GetString("Main.Navigation.Export");

                if (exportSubItem.Items.Count > 0 && exportSubItem.Items[0] is MenuFlyoutItem exportCsvItem)
                {
                    exportCsvItem.Text = _localizationService.GetString("Button.ExportCsv");
                }

                if (exportSubItem.Items.Count > 1 && exportSubItem.Items[1] is MenuFlyoutItem exportGoogleCsvItem)
                {
                    exportGoogleCsvItem.Text = _localizationService.GetString("Export.Format.GoogleCsv");
                }

                if (exportSubItem.Items.Count > 2 && exportSubItem.Items[2] is MenuFlyoutItem exportOutlookCsvItem)
                {
                    exportOutlookCsvItem.Text = _localizationService.GetString("Export.Format.OutlookCsv");
                }

                if (exportSubItem.Items.Count > 3 && exportSubItem.Items[3] is MenuFlyoutItem exportVCardItem)
                {
                    exportVCardItem.Text = _localizationService.GetString("Export.Format.VCard");
                }

                if (exportSubItem.Items.Count > 4 && exportSubItem.Items[4] is MenuFlyoutItem exportTxtItem)
                {
                    exportTxtItem.Text = _localizationService.GetString("Button.ExportTxt");
                }
            }

            if (flyout.Items[2] is MenuFlyoutItem deleteItem)
            {
                deleteItem.Text = _localizationService.GetString("Button.Delete");
            }
        }

        private void NavigateToDetail(BusinessCard? card)
        {
            if (card == null || Frame == null)
            {
                return;
            }

            var navParams = new NavigationParams
            {
                AllCards = ViewModel.AllCards,
                SelectedCard = card
            };

            Frame.Navigate(typeof(CardDetailPage), navParams, new SuppressNavigationTransitionInfo());
        }

        private async void OnExportCsvContextClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            await ExportCardAsync(sender, ContactExportFormat.Csv);
        }

        private async void OnExportSelectedCsvClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            await ViewModel.ExportSelectedAsync(ContactExportFormat.Csv);
        }

        private async void OnExportSelectedGoogleCsvClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            await ViewModel.ExportSelectedAsync(ContactExportFormat.GoogleCsv);
        }

        private async void OnExportSelectedOutlookCsvClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            await ViewModel.ExportSelectedAsync(ContactExportFormat.OutlookCsv);
        }

        private async void OnExportSelectedVCardClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            await ViewModel.ExportSelectedAsync(ContactExportFormat.VCard);
        }

        private async void OnExportSelectedPlainTextClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            await ViewModel.ExportSelectedAsync(ContactExportFormat.PlainText);
        }

        private async void OnExportGoogleCsvContextClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            await ExportCardAsync(sender, ContactExportFormat.GoogleCsv);
        }

        private async void OnExportOutlookCsvContextClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            await ExportCardAsync(sender, ContactExportFormat.OutlookCsv);
        }

        private async void OnExportVCardContextClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            await ExportCardAsync(sender, ContactExportFormat.VCard);
        }

        private async Task ExportCardAsync(object sender, ContactExportFormat format)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is BusinessCard card)
            {
                await ViewModel.ExportSingleCardAsync(card, format);
            }
        }

        private async void OnExportTxtContextClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is BusinessCard card)
            {
                await ViewModel.ExportSingleCardAsync(card, ContactExportFormat.PlainText);
            }
        }

        private async void OnAiReprocessClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (ViewModel.SelectedCard == null)
            {
                return;
            }

            await ViewModel.ReprocessAiAsync(ViewModel.SelectedCard);
        }

        private async void OnAddSidebarTagClicked(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedCard == null)
            {
                return;
            }

            var flyout = new MenuFlyout
            {
                MenuFlyoutPresenterStyle = (Style)Application.Current.Resources["BcrTagMenuFlyoutPresenterStyle"]
            };
            var available = _tagCatalogService.GetAllTags()
                .Where(tag => !TagTextHelper.ContainsIgnoreCase(SidebarSelectedTags, tag))
                .ToList();

            foreach (var tag in available)
            {
                var item = new MenuFlyoutItem { Text = tag, Tag = tag };
                item.Click += async (_, __) =>
                {
                    TagTextHelper.AddIfMissing(SidebarSelectedTags, tag);
                    RebuildSidebarTagFlowItems();
                    await PersistSidebarTagsAsync();
                };
                flyout.Items.Add(item);
            }

            if (available.Count > 0)
            {
                flyout.Items.Add(new MenuFlyoutSeparator());
            }

            var newTagItem = new MenuFlyoutItem { Text = _localizationService.GetString("Tag.New") };
            newTagItem.Click += async (_, __) =>
            {
                var value = await TagDialogHelper.PromptForNewTagAsync(this.XamlRoot);
                if (string.IsNullOrWhiteSpace(value))
                {
                    return;
                }

                if (TagTextHelper.AddIfMissing(SidebarSelectedTags, value))
                {
                    RebuildSidebarTagFlowItems();
                }

                _tagCatalogService.AddTag(value);
                await PersistSidebarTagsAsync();
                await _tagCatalogService.SaveAsync();
            };

            flyout.Items.Add(newTagItem);
            flyout.ShowAt((FrameworkElement)sender);
        }

        private async void OnRemoveSidebarTagClicked(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string tag)
            {
                return;
            }

            if (!TagTextHelper.RemoveFirstIgnoreCase(SidebarSelectedTags, tag))
            {
                return;
            }

            RebuildSidebarTagFlowItems();
            await PersistSidebarTagsAsync();
        }

        private async Task PersistSidebarTagsAsync()
        {
            if (ViewModel.SelectedCard == null)
            {
                return;
            }

            ViewModel.SelectedCard.Tag = TagTextHelper.Join(SidebarSelectedTags);

            var hasNew = false;
            foreach (var tag in SidebarSelectedTags)
            {
                if (_tagCatalogService.AddTag(tag))
                {
                    hasNew = true;
                }
            }

            if (hasNew)
            {
                await _tagCatalogService.SaveAsync();
            }
        }

        private void RefreshSidebarTagsFromCard()
        {
            SidebarSelectedTags.Clear();
            var tags = TagTextHelper.Split(ViewModel.SelectedCard?.Tag);
            foreach (var tag in tags)
            {
                TagTextHelper.AddIfMissing(SidebarSelectedTags, tag);
            }
            RebuildSidebarTagFlowItems();
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AllCardsViewModel.SelectedCard))
            {
                RefreshSidebarTagsFromCard();
            }

            if (e.PropertyName == nameof(AllCardsViewModel.IsBatchExportMode))
            {
                UpdateBatchExportSelectionMode();
            }

            if (ViewModel.IsBatchExportMode
                && (e.PropertyName is nameof(AllCardsViewModel.SelectedExportCount)
                    or nameof(AllCardsViewModel.GroupedCards)))
            {
                QueueExportSelectionSynchronization();
            }
        }

        private void OnCardSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSynchronizingExportSelection || !ViewModel.IsBatchExportMode)
            {
                return;
            }

            foreach (var card in e.RemovedItems.OfType<BusinessCard>())
            {
                ViewModel.SetCardExportSelected(card, false);
            }

            foreach (var card in e.AddedItems.OfType<BusinessCard>())
            {
                ViewModel.SetCardExportSelected(card, true);
            }

            QueueExportSelectionSynchronization();
        }

        private void UpdateBatchExportSelectionMode()
        {
            _isSynchronizingExportSelection = true;
            try
            {
                var selectionMode = ViewModel.IsBatchExportMode
                    ? ListViewSelectionMode.Multiple
                    : ListViewSelectionMode.None;
                CardGridView.SelectionMode = selectionMode;
                CardListView.SelectionMode = selectionMode;
            }
            finally
            {
                _isSynchronizingExportSelection = false;
            }

            QueueExportSelectionSynchronization();
        }

        private void QueueExportSelectionSynchronization()
        {
            if (!ViewModel.IsBatchExportMode || _isExportSelectionSyncQueued)
            {
                return;
            }

            _isExportSelectionSyncQueued = true;
            if (!DispatcherQueue.TryEnqueue(() =>
            {
                _isExportSelectionSyncQueued = false;
                SynchronizeExportSelection();
            }))
            {
                _isExportSelectionSyncQueued = false;
            }
        }

        private void SynchronizeExportSelection()
        {
            if (_isSynchronizingExportSelection || !ViewModel.IsBatchExportMode)
            {
                return;
            }

            _isSynchronizingExportSelection = true;
            try
            {
                SynchronizeExportSelection(CardGridView);
                SynchronizeExportSelection(CardListView);
            }
            finally
            {
                _isSynchronizingExportSelection = false;
            }
        }

        private void SynchronizeExportSelection(ListViewBase listView)
        {
            listView.SelectedItems.Clear();
            foreach (var card in ViewModel.FilteredCards.Where(ViewModel.IsCardSelectedForExport))
            {
                listView.SelectedItems.Add(card);
            }
        }

        private void OnExportInfoBarClosed(InfoBar sender, InfoBarClosedEventArgs args)
        {
            ViewModel.DismissExportMessageCommand.Execute(null);
        }

        private void OnTagCatalogChanged()
        {
            DispatcherQueue.TryEnqueue(RefreshSidebarTagsFromCard);
        }

        private void OnDetailFieldTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string fieldKey)
            {
                ViewModel.BeginFieldEdit(fieldKey);
            }
        }

        private void OnSimpleFieldEditStarted(object? sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(ViewModel.EditingFieldKey))
            {
                ViewModel.EndFieldEdit(ViewModel.EditingFieldKey);
            }
        }

        private void OnSidebarContentPointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var origin = e.OriginalSource as Microsoft.UI.Xaml.DependencyObject;

            if (!string.IsNullOrEmpty(ViewModel.EditingFieldKey))
            {
                var activeContainer = GetFieldEditContainer(ViewModel.EditingFieldKey);
                if (activeContainer != null && !IsDescendantOf(origin, activeContainer))
                {
                    ViewModel.EndFieldEdit(ViewModel.EditingFieldKey);
                }
            }

            if (ShouldRedirectFocusToSink(origin))
            {
                SidebarFocusSink.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
            }
        }

        private void OnCompositeFieldLosingFocus(UIElement sender, Microsoft.UI.Xaml.Input.LosingFocusEventArgs e)
        {
            if (sender is not FrameworkElement element)
            {
                return;
            }

            var fieldKey = ResolveFieldKey(element);
            if (fieldKey == null)
            {
                return;
            }

            var container = GetFieldEditContainer(fieldKey);
            if (container == null)
            {
                return;
            }

            if (e.NewFocusedElement is Microsoft.UI.Xaml.DependencyObject nextFocused
                && IsDescendantOf(nextFocused, container))
            {
                return;
            }

            DispatcherQueue.TryEnqueue(() =>
            {
                var focused = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(XamlRoot) as Microsoft.UI.Xaml.DependencyObject;
                if (focused != null && IsDescendantOf(focused, container))
                {
                    return;
                }

                ViewModel.EndFieldEdit(fieldKey);
            });
        }

        private bool ShouldRedirectFocusToSink(Microsoft.UI.Xaml.DependencyObject? origin)
        {
            var focused = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(XamlRoot) as Microsoft.UI.Xaml.DependencyObject;
            if (focused == null)
            {
                return false;
            }

            if (!IsDescendantOfType<TextBox>(focused) && !IsDescendantOfType<EditableField>(focused))
            {
                return false;
            }

            return !IsDescendantOfType<TextBox>(origin)
                && !IsDescendantOfType<Button>(origin)
                && !IsDescendantOfType<EditableField>(origin);
        }

        private string? ResolveFieldKey(FrameworkElement element)
        {
            if (IsDescendantOf(element, NameEditContainer))
            {
                return "Name";
            }

            if (IsDescendantOf(element, DepartmentEditContainer))
            {
                return "Department";
            }

            if (IsDescendantOf(element, AddressEditContainer))
            {
                return "Address";
            }

            if (IsDescendantOf(element, TelephoneEditContainer))
            {
                return "Telephone";
            }

            return null;
        }

        private FrameworkElement? GetFieldEditContainer(string fieldKey)
        {
            return fieldKey switch
            {
                "Name" => NameEditContainer,
                "Department" => DepartmentEditContainer,
                "Address" => AddressEditContainer,
                "Telephone" => TelephoneEditContainer,
                _ => null
            };
        }

        private static bool IsDescendantOf(Microsoft.UI.Xaml.DependencyObject? element, Microsoft.UI.Xaml.DependencyObject? ancestor)
        {
            while (element != null)
            {
                if (element == ancestor)
                {
                    return true;
                }

                element = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(element);
            }

            return false;
        }

        private static bool IsDescendantOfType<T>(Microsoft.UI.Xaml.DependencyObject? element)
            where T : Microsoft.UI.Xaml.DependencyObject
        {
            while (element != null)
            {
                if (element is T)
                {
                    return true;
                }

                element = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(element);
            }

            return false;
        }

        private void RebuildSidebarTagFlowItems()
        {
            CardPageUiHelper.RebuildTagFlowItems(SidebarTagFlowItems, SidebarSelectedTags);
        }

        private void OnLanguageChanged()
        {
            DispatcherQueue.TryEnqueue(Bindings.Update);
        }
    }
}
