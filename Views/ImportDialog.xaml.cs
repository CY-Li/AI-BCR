#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PlustekBCR.Helpers;
using PlustekBCR.Models;
using PlustekBCR.Services;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace PlustekBCR.Views
{
    public sealed partial class ImportDialog : ContentDialog
    {
        private const string DropZoneActiveBrushKey = "BrandBlueBrush";
        private const string DropZoneIdleBrushKey = "BorderBrush";
        private static readonly Thickness DropZoneActiveBorderThickness = new(2);
        private static readonly Thickness DropZoneIdleBorderThickness = new(1.5);

        private string? _excelCsvFilePath;
        private List<string> _csvHeaders = new();
        private readonly IBusinessCardFieldService _fieldService;
        private UIElement? _popupRoot;
        private Microsoft.UI.Xaml.Input.PointerEventHandler? _pointerHandler;

        public ImportDialog()
        {
            _fieldService = App.GetService<IBusinessCardFieldService>();
            InitializeComponent();

            Opened += ImportDialog_Opened;
            Closed += ImportDialog_Closed;
        }

        private void ImportDialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            DependencyObject current = this;
            DependencyObject root = this;
            while (current != null)
            {
                root = current;
                current = VisualTreeHelper.GetParent(current);
            }

            if (root is UIElement uiRoot)
            {
                _popupRoot = uiRoot;
                _pointerHandler = new Microsoft.UI.Xaml.Input.PointerEventHandler(ImportDialog_PointerPressed);
                _popupRoot.AddHandler(UIElement.PointerPressedEvent, _pointerHandler, true);
            }
        }

        private void ImportDialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            if (_popupRoot != null && _pointerHandler != null)
            {
                _popupRoot.RemoveHandler(UIElement.PointerPressedEvent, _pointerHandler);
                _popupRoot = null;
                _pointerHandler = null;
            }
        }

        private void ImportDialog_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var position = e.GetCurrentPoint(DialogRootGrid).Position;
            if (position.X < 0 || position.Y < 0 || position.X > DialogRootGrid.ActualWidth || position.Y > DialogRootGrid.ActualHeight)
            {
                Hide();
            }
        }

        #region DRAG AND DROP SENSING
        private void OnSpreadsheetDragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            ApplyDropZoneActiveState(SpreadsheetDropBorder, SpreadsheetScale);
            e.Handled = true;
        }

        private void OnSpreadsheetDragLeave(object sender, DragEventArgs e)
        {
            ResetDropZoneState(SpreadsheetDropBorder, SpreadsheetScale);
        }

        private async void OnSpreadsheetDrop(object sender, DragEventArgs e)
        {
            OnSpreadsheetDragLeave(null!, null!);
            e.Handled = true;
            if (!e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
            {
                return;
            }

            var deferral = e.GetDeferral();
            try
            {
                var items = await e.DataView.GetStorageItemsAsync();
                var files = items.OfType<StorageFile>().ToList();
                var spreadsheet = files.FirstOrDefault(f => f.FileType.Equals(".csv", StringComparison.OrdinalIgnoreCase) || f.FileType.Equals(".xlsx", StringComparison.OrdinalIgnoreCase));
                if (spreadsheet != null)
                {
                    await ProcessSpreadsheetFileAsync(spreadsheet);
                }
            }
            finally
            {
                deferral.Complete();
            }
        }

        private void OnImagesDragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            ApplyDropZoneActiveState(ImagesDropBorder, ImagesScale);
            e.Handled = true;
        }

        private void OnImagesDragLeave(object sender, DragEventArgs e)
        {
            ResetDropZoneState(ImagesDropBorder, ImagesScale);
        }

        private async void OnImagesDrop(object sender, DragEventArgs e)
        {
            OnImagesDragLeave(null!, null!);
            e.Handled = true;
            if (!e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
            {
                return;
            }

            var deferral = e.GetDeferral();
            try
            {
                var items = await e.DataView.GetStorageItemsAsync();
                var files = items.OfType<StorageFile>().ToList();
                var images = files.Where(f =>
                    f.FileType.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                    || f.FileType.Equals(".png", StringComparison.OrdinalIgnoreCase)
                    || f.FileType.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
                    || f.FileType.Equals(".heic", StringComparison.OrdinalIgnoreCase)).ToList();

                if (images.Count > 0)
                {
                    await ProcessImageFilesAsync(images);
                }
            }
            finally
            {
                deferral.Complete();
            }
        }
        #endregion

        private static Brush GetAppBrush(string resourceKey)
        {
            return (Brush)Application.Current.Resources[resourceKey];
        }

        private static void ApplyDropZoneActiveState(Border dropZone, ScaleTransform scale)
        {
            dropZone.BorderBrush = GetAppBrush(DropZoneActiveBrushKey);
            dropZone.BorderThickness = DropZoneActiveBorderThickness;
            scale.ScaleX = 1.01;
            scale.ScaleY = 1.01;
        }

        private static void ResetDropZoneState(Border dropZone, ScaleTransform scale)
        {
            dropZone.BorderBrush = GetAppBrush(DropZoneIdleBrushKey);
            dropZone.BorderThickness = DropZoneIdleBorderThickness;
            scale.ScaleX = 1;
            scale.ScaleY = 1;
        }

        #region FILE PICKERS & PARSING
        private async void OnSelectExcelCsvClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = CreateOpenPicker(
                    PickerViewMode.List,
                    PickerLocationId.DocumentsLibrary,
                    ".csv",
                    ".xlsx");

                var file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    await ProcessSpreadsheetFileAsync(file);
                }
            }
            catch (Exception ex)
            {
                LogImportError("Error selecting spreadsheet", ex);
            }
        }

        private async void OnSelectImagesClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = CreateOpenPicker(
                    PickerViewMode.Thumbnail,
                    PickerLocationId.PicturesLibrary,
                    ".jpg",
                    ".jpeg",
                    ".png",
                    ".heic");

                var files = await picker.PickMultipleFilesAsync();
                if (files != null && files.Count > 0)
                {
                    await ProcessImageFilesAsync(files.ToList());
                }
            }
            catch (Exception ex)
            {
                LogImportError("Error selecting images", ex);
            }
        }

        private async Task ProcessSpreadsheetFileAsync(StorageFile file)
        {
            ShowLoading("Reading file contents...");
            _excelCsvFilePath = file.Path;

            await Task.Run(() => { _csvHeaders = CsvHelper.ReadHeaders(_excelCsvFilePath); });

            if (_csvHeaders.Count == 0)
            {
                HideLoading();
                await ShowMessageDialogAsync(
                    "Unsupported or Empty File",
                    "The spreadsheet file does not contain valid column headers. Please use the download template to ensure proper formatting.");
                return;
            }

            ShowLoading("Importing structural database rows...");

            var rawRows = new List<Dictionary<string, string>>();
            await Task.Run(() => { rawRows = CsvHelper.ReadAllRows(_excelCsvFilePath); });

            var importedCards = new List<BusinessCard>();
            var importFields = _fieldService.GetFields(BusinessCardSurface.Import);

            foreach (var row in rawRows)
            {
                var card = new BusinessCard
                {
                    ScanDate = DateTime.Now,
                    Status = ProcessingStatus.Manual,
                    MarketCode = _fieldService.CurrentMarket
                };

                foreach (var field in importFields)
                {
                    var matchedHeader = FindBestHeaderMatch(field.Key, _csvHeaders);
                    if (matchedHeader == null)
                    {
                        continue;
                    }

                    var cellValue = row.TryGetValue(matchedHeader, out var value) ? value : string.Empty;
                    BusinessCardFieldAccessor.SetTextValue(card, field.PropertyName, cellValue);
                }

                BusinessCardFieldAccessor.SetIdentityFromFullName(card);
                card.PopulateDerivedFieldsFromStructuredValues();

                if (string.IsNullOrWhiteSpace(card.FullName))
                {
                    card.FullName = "Imported Contact";
                }

                importedCards.Add(card);
            }

            WeakReferenceMessenger.Default.Send(new CardsImportedMessage(importedCards));

            HideLoading();
            Hide();
        }

        private async Task ProcessImageFilesAsync(List<StorageFile> files)
        {
            ShowLoading("Ingesting card images to queue...");

            var importedCards = new List<BusinessCard>();

            foreach (var file in files)
            {
                byte[]? imageBytes = null;
                try
                {
                    imageBytes = await Task.Run(() => File.ReadAllBytes(file.Path));
                }
                catch (Exception ex)
                {
                    LogImportError("Failed reading image byte array", ex);
                }

                var card = new BusinessCard
                {
                    ScanDate = DateTime.Now,
                    MarketCode = _fieldService.CurrentMarket,
                    FrontImageData = imageBytes,
                    Status = ProcessingStatus.Pending,
                    FullName = Path.GetFileNameWithoutExtension(file.Name),
                    CompanyName = "Ingested Image"
                };

                BusinessCardFieldAccessor.SetIdentityFromFullName(card);
                importedCards.Add(card);
            }

            WeakReferenceMessenger.Default.Send(new CardsImportedMessage(importedCards));
            HideLoading();
            Hide();
        }
        #endregion

        #region HELPERS & FUZZY AUTO-MAPPING
        private string? FindBestHeaderMatch(string field, List<string> headers)
        {
            var lowerField = field.ToLowerInvariant();
            foreach (var header in headers)
            {
                var lowerHeader = header.ToLowerInvariant().Trim();
                if (lowerHeader == lowerField)
                {
                    return header;
                }

                if (lowerField == "full_name" && (lowerHeader == "name" || lowerHeader.Contains("full name")))
                {
                    return header;
                }

                if (lowerField == "job_title" && (lowerHeader == "title" || lowerHeader.Contains("job")))
                {
                    return header;
                }

                if (lowerField == "company_name" && (lowerHeader == "company" || lowerHeader.Contains("org")))
                {
                    return header;
                }

                if (lowerField == "tel" && (lowerHeader == "phone" || lowerHeader.Contains("telephone")))
                {
                    return header;
                }

                if (lowerField == "mobile" && (lowerHeader.Contains("mobile") || lowerHeader.Contains("cell")))
                {
                    return header;
                }

                if (lowerField == "full_address" && (lowerHeader == "address" || lowerHeader.Contains("addr")))
                {
                    return header;
                }
            }

            foreach (var header in headers)
            {
                var lowerHeader = header.ToLowerInvariant();
                if (lowerHeader.Contains(lowerField) || lowerField.Contains(lowerHeader))
                {
                    return header;
                }
            }

            return null;
        }

        private async void OnDownloadTemplateClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = CreateSavePicker(
                    PickerLocationId.Downloads,
                    "business_cards_template",
                    "CSV (Comma delimited)",
                    ".csv");

                var file = await picker.PickSaveFileAsync();
                if (file != null)
                {
                    var headers = _fieldService.GetCsvHeaders(BusinessCardSurface.Import);
                    string headerRow = string.Join(",", headers) + Environment.NewLine;
                    await File.WriteAllTextAsync(file.Path, headerRow);
                }
            }
            catch (Exception ex)
            {
                LogImportError("Failed to save template", ex);
            }
        }

        private void ShowLoading(string text)
        {
            LoadingStatusText.Text = text;
            LoadingShield.Visibility = Visibility.Visible;
        }

        private void HideLoading()
        {
            LoadingShield.Visibility = Visibility.Collapsed;
        }

        private static FileOpenPicker CreateOpenPicker(PickerViewMode viewMode, PickerLocationId startLocation, params string[] fileTypes)
        {
            var picker = new FileOpenPicker
            {
                ViewMode = viewMode,
                SuggestedStartLocation = startLocation
            };

            foreach (var fileType in fileTypes)
            {
                picker.FileTypeFilter.Add(fileType);
            }

            PickerWindowHelper.Initialize(picker);
            return picker;
        }

        private static FileSavePicker CreateSavePicker(
            PickerLocationId startLocation,
            string suggestedFileName,
            string typeLabel,
            string extension)
        {
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = startLocation,
                SuggestedFileName = suggestedFileName
            };
            picker.FileTypeChoices.Add(typeLabel, new List<string> { extension });

            PickerWindowHelper.Initialize(picker);
            return picker;
        }

        private async Task ShowMessageDialogAsync(string title, string content)
        {
            await DialogHelper.ShowMessageAsync(XamlRoot, title, content);
        }

        private static void LogImportError(string message, Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"{message}: {ex.Message}");
        }
        #endregion
    }
}
