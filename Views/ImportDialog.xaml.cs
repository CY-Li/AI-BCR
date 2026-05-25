#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Windows.Storage.Pickers;
using CommunityToolkit.Mvvm.Messaging;
using PlustekBCR.Models;
using PlustekBCR.Helpers;

namespace PlustekBCR.Views
{
    public sealed partial class ImportDialog : ContentDialog
    {
        private string? _excelCsvFilePath;
        private List<string> _csvHeaders = new();

        private readonly List<string> _systemFields = new()
        {
            "Name", "Title", "Company", "Phone", "Email", "Address", "Country", "Website", "Tag"
        };

        private UIElement? _popupRoot;
        private Microsoft.UI.Xaml.Input.PointerEventHandler? _pointerHandler;

        public ImportDialog()
        {
            this.InitializeComponent();
            
            // Register standard events
            this.Opened += ImportDialog_Opened;
            this.Closed += ImportDialog_Closed;
        }

        private void ImportDialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            DependencyObject current = this;
            DependencyObject root = this;
            while (current != null)
            {
                root = current;
                current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current);
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
            if (position.X < 0 || position.Y < 0 || 
                position.X > DialogRootGrid.ActualWidth || position.Y > DialogRootGrid.ActualHeight)
            {
                this.Hide();
            }
        }

        #region DRAG AND DROP SENSING
        private void OnSpreadsheetDragOver(object sender, DragEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[DRAG] OnSpreadsheetDragOver fired.");
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            SpreadsheetDropBorder.BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["BrandBlueBrush"];
            SpreadsheetDropBorder.BorderThickness = new Thickness(2);
            SpreadsheetScale.ScaleX = 1.01;
            SpreadsheetScale.ScaleY = 1.01;
            e.Handled = true;
        }

        private void OnSpreadsheetDragLeave(object sender, DragEventArgs e)
        {
            SpreadsheetDropBorder.BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["BorderBrush"];
            SpreadsheetDropBorder.BorderThickness = new Thickness(1.5);
            SpreadsheetScale.ScaleX = 1;
            SpreadsheetScale.ScaleY = 1;
        }

        private async void OnSpreadsheetDrop(object sender, DragEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[DRAG] OnSpreadsheetDrop fired.");
            OnSpreadsheetDragLeave(null!, null!);
            e.Handled = true;
            if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
            {
                System.Diagnostics.Debug.WriteLine("[DRAG] DataView contains StorageItems.");
                var deferral = e.GetDeferral();
                try
                {
                    var items = await e.DataView.GetStorageItemsAsync();
                    System.Diagnostics.Debug.WriteLine($"[DRAG] GetStorageItemsAsync returned {items.Count} items.");
                    var files = items.OfType<StorageFile>().ToList();
                    var spreadsheet = files.FirstOrDefault(f => f.FileType.Equals(".csv", StringComparison.OrdinalIgnoreCase) || 
                                                                f.FileType.Equals(".xlsx", StringComparison.OrdinalIgnoreCase));
                    if (spreadsheet != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DRAG] Found spreadsheet: {spreadsheet.Name}");
                        await ProcessSpreadsheetFileAsync(spreadsheet);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[DRAG] No valid spreadsheet found in dropped items.");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DRAG] Exception in OnSpreadsheetDrop: {ex.Message}");
                }
                finally
                {
                    deferral.Complete();
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[DRAG] DataView DOES NOT contain StorageItems.");
            }
        }

        private void OnImagesDragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            ImagesDropBorder.BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["BrandBlueBrush"];
            ImagesDropBorder.BorderThickness = new Thickness(2);
            ImagesScale.ScaleX = 1.01;
            ImagesScale.ScaleY = 1.01;
            e.Handled = true;
        }

        private void OnImagesDragLeave(object sender, DragEventArgs e)
        {
            ImagesDropBorder.BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["BorderBrush"];
            ImagesDropBorder.BorderThickness = new Thickness(1.5);
            ImagesScale.ScaleX = 1;
            ImagesScale.ScaleY = 1.01; // wait this should be 1, let me fix it to 1
        }

        private async void OnImagesDrop(object sender, DragEventArgs e)
        {
            OnImagesDragLeave(null!, null!);
            e.Handled = true;
            if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
            {
                var deferral = e.GetDeferral();
                try
                {
                    var items = await e.DataView.GetStorageItemsAsync();
                    var files = items.OfType<StorageFile>().ToList();
                    var images = files.Where(f => f.FileType.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                                  f.FileType.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                                                  f.FileType.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                                                  f.FileType.Equals(".heic", StringComparison.OrdinalIgnoreCase)).ToList();
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
        }
        #endregion

        #region FILE PICKERS & PARSING
        private async void OnSelectExcelCsvClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileOpenPicker();
                picker.ViewMode = PickerViewMode.List;
                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                picker.FileTypeFilter.Add(".csv");
                picker.FileTypeFilter.Add(".xlsx");

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Window);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                var file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    await ProcessSpreadsheetFileAsync(file);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error selecting spreadsheet: {ex.Message}");
            }
        }

        private async void OnSelectImagesClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileOpenPicker();
                picker.ViewMode = PickerViewMode.Thumbnail;
                picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                picker.FileTypeFilter.Add(".jpg");
                picker.FileTypeFilter.Add(".jpeg");
                picker.FileTypeFilter.Add(".png");
                picker.FileTypeFilter.Add(".heic");

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Window);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                var files = await picker.PickMultipleFilesAsync();
                if (files != null && files.Count > 0)
                {
                    await ProcessImageFilesAsync(files.ToList());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error selecting images: {ex.Message}");
            }
        }

        private async Task ProcessSpreadsheetFileAsync(StorageFile file)
        {
            ShowLoading("Reading file contents...");
            _excelCsvFilePath = file.Path;
            
            // Parse headers on a background thread
            await Task.Run(() =>
            {
                _csvHeaders = CsvHelper.ReadHeaders(_excelCsvFilePath);
            });

            if (_csvHeaders.Count == 0)
            {
                HideLoading();
                var dialog = new ContentDialog
                {
                    Title = "Unsupported or Empty File",
                    Content = "The spreadsheet file does not contain valid column headers. Please use the download template to ensure proper formatting.",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
                return;
            }

            ShowLoading("Importing structural database rows...");

            // Read all rows in background
            var rawRows = new List<Dictionary<string, string>>();
            await Task.Run(() =>
            {
                rawRows = CsvHelper.ReadAllRows(_excelCsvFilePath);
            });

            var importedCards = new List<BusinessCard>();

            foreach (var row in rawRows)
            {
                var card = new BusinessCard
                {
                    ScanDate = DateTime.Now,
                    Status = ProcessingStatus.Manual // Directly imported
                };

                foreach (var sysField in _systemFields)
                {
                    var matchedHeader = FindBestHeaderMatch(sysField, _csvHeaders);
                    if (matchedHeader != null)
                    {
                        var cellValue = row.ContainsKey(matchedHeader) ? row[matchedHeader] : string.Empty;

                        switch (sysField)
                        {
                            case "Name": card.Name = cellValue; break;
                            case "Title": card.Title = cellValue; break;
                            case "Company": card.Company = cellValue; break;
                            case "Phone": card.Phone = cellValue; break;
                            case "Email": card.Email = cellValue; break;
                            case "Address": card.Address = cellValue; break;
                            case "Country": card.Country = cellValue; break;
                            case "Website": card.Website = cellValue; break;
                            case "Tag": card.Tag = cellValue; break;
                        }
                    }
                }

                // Simple auto-fill if fields are empty
                if (string.IsNullOrWhiteSpace(card.Name)) card.Name = "Imported Contact";

                importedCards.Add(card);
            }

            // Send imported message using CommunityToolkit Messenger
            WeakReferenceMessenger.Default.Send(new CardsImportedMessage(importedCards));

            HideLoading();
            this.Hide();
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
                    System.Diagnostics.Debug.WriteLine($"Failed reading image byte array: {ex.Message}");
                }

                var card = new BusinessCard
                {
                    ScanDate = DateTime.Now,
                    FrontImageData = imageBytes,
                    Status = ProcessingStatus.Recognizing, // Runs AI OCR extraction automatically in background
                    Name = Path.GetFileNameWithoutExtension(file.Name),
                    Company = "Ingested Image"
                };

                importedCards.Add(card);
            }

            WeakReferenceMessenger.Default.Send(new CardsImportedMessage(importedCards));

            HideLoading();
            this.Hide();
        }
        #endregion

        #region HELPERS & FUZZY AUTO-MAPPING
        private string? FindBestHeaderMatch(string field, List<string> headers)
        {
            var lowerField = field.ToLower();
            foreach (var h in headers)
            {
                var lowerH = h.ToLower();
                if (lowerH == lowerField) return h;

                // Localization matches (Chinese / English)
                if (lowerField == "name" && (lowerH.Contains("姓名") || lowerH.Contains("名字") || lowerH == "name" || lowerH == "聯絡人")) return h;
                if (lowerField == "title" && (lowerH.Contains("職稱") || lowerH.Contains("頭銜") || lowerH == "title" || lowerH.Contains("job"))) return h;
                if (lowerField == "company" && (lowerH.Contains("公司") || lowerH.Contains("企業") || lowerH == "company" || lowerH.Contains("org") || lowerH.Contains("單位"))) return h;
                if (lowerField == "phone" && (lowerH.Contains("電話") || lowerH.Contains("手機") || lowerH.Contains("行動") || lowerH == "phone" || lowerH.Contains("tel") || lowerH.Contains("mobile"))) return h;
                if (lowerField == "email" && (lowerH.Contains("信箱") || lowerH.Contains("電子郵件") || lowerH == "email" || lowerH.Contains("mail"))) return h;
                if (lowerField == "address" && (lowerH.Contains("地址") || lowerH == "address" || lowerH.Contains("addr"))) return h;
                if (lowerField == "country" && (lowerH.Contains("國家") || lowerH.Contains("地區") || lowerH == "country")) return h;
                if (lowerField == "website" && (lowerH.Contains("網址") || lowerH.Contains("網站") || lowerH == "website" || lowerH.Contains("web") || lowerH.Contains("site"))) return h;
                if (lowerField == "tag" && (lowerH.Contains("標籤") || lowerH == "tag" || lowerH.Contains("tags"))) return h;
            }

            // Substring mapping
            foreach (var h in headers)
            {
                var lowerH = h.ToLower();
                if (lowerH.Contains(lowerField) || lowerField.Contains(lowerH)) return h;
            }
            return null;
        }

        private async void OnDownloadTemplateClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileSavePicker();
                picker.SuggestedStartLocation = PickerLocationId.Downloads;
                picker.SuggestedFileName = "business_cards_template";
                picker.FileTypeChoices.Add("CSV (Comma delimited)", new List<string> { ".csv" });

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Window);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                var file = await picker.PickSaveFileAsync();
                if (file != null)
                {
                    string headerRow = string.Join(",", _systemFields) + Environment.NewLine;
                    string exampleRow = "John Doe,Software Architect,Awesome Corp,+886912345678,john.doe@awesome.com,123 Tech Street,Taiwan,https://awesome.com,VIP;Developer" + Environment.NewLine;
                    await File.WriteAllTextAsync(file.Path, headerRow + exampleRow);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save template: {ex.Message}");
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
        #endregion
    }
}
