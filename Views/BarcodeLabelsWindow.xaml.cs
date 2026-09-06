using MyWPFCRUDApp.Helpers;
using MyWPFCRUDApp.Models;
using MyWPFCRUDApp.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace MyWPFCRUDApp.Views
{
    public partial class BarcodeLabelsWindow : Window
    {
        private readonly ProductService _productService;
        private readonly ObservableCollection<BarcodeColumnOption> _columnOptions;
        private readonly Dictionary<BarcodeColumnOption, DataGridColumn> _activeOptionalColumns = new();
        private ObservableCollection<BarcodeLabelRow> _rows = new();

        public BarcodeLabelsWindow(IEnumerable<MPurchaseDetail> items, ProductService? productService = null)
        {
            InitializeComponent();
            _productService = productService ?? new ProductService();

            _rows = new ObservableCollection<BarcodeLabelRow>(
                items
                    .Where(i => !string.IsNullOrWhiteSpace(i.Barcode))
                    .Select(BuildRow));

            LabelsGrid.ItemsSource = _rows;

            _columnOptions = BuildColumnOptions();
            ColumnPicker.ItemsSource = _columnOptions;
            OnColumnPickerLoaded();

            LoadCustomTemplates();

            if (_rows.Any())
                LabelsGrid.SelectedIndex = 0;
            else
                NoSelectionHint.Text = "No barcoded items on this invoice.";

            TemplateList.SelectedIndex = 0;
            UpdatePreviewVisibility();
        }

        private BarcodeLabelRow BuildRow(MPurchaseDetail item)
        {
            var product = _productService.GetByBarcode(item.Barcode);
            return new BarcodeLabelRow
            {
                Barcode = item.Barcode,
                Quantity = item.Quantity,
                ProductName = item.ProductName,
                MRP = item.MRP,
                Retail = item.Retail,
                PurchasePrice = item.PurchasePrice,
                WholesalePrice = item.WholesalePrice,
                BarcodeImage = BarcodeImageHelper.GenerateCode128(item.Barcode),
                ProductCode = product?.ProductCode,
                HSNCode = product?.HSNCode ?? item.HSNCode,
                Size = product?.Size ?? item.Size,
                Colour = product?.Colour ?? item.Colour,
                Batch = product?.Batch,
                MfgDate = product?.MfgDate,
                ExpDate = product?.ExpDate,
                Godown = product?.Godown,
                Rack = product?.Rack,
                PartGroup = product?.PartGroup,
                Description = product?.Description,
                DiscountPercentage = product?.DiscountPercentage ?? 0,
                CGST = product?.CGST ?? (double)item.CGST,
                SGST = product?.SGST ?? (double)item.SGST,
                IGST = product?.IGST ?? (double)item.IGST,
                CESS = product?.CESS ?? 0
            };
        }

        private static ObservableCollection<BarcodeColumnOption> BuildColumnOptions()
        {
            return new ObservableCollection<BarcodeColumnOption>
            {
                new() { Header = "MRP",          BindingPath = nameof(BarcodeLabelRow.MRP),          StringFormat = "N2", Width = 90,  IsChecked = true },
                new() { Header = "Retail",       BindingPath = nameof(BarcodeLabelRow.Retail),       StringFormat = "N2", Width = 90,  IsChecked = true },
                new() { Header = "Purchase Price", BindingPath = nameof(BarcodeLabelRow.PurchasePrice), StringFormat = "N2", Width = 100 },
                new() { Header = "Wholesale",    BindingPath = nameof(BarcodeLabelRow.WholesalePrice), StringFormat = "N2", Width = 100 },
                new() { Header = "Product Code", BindingPath = nameof(BarcodeLabelRow.ProductCode),  Width = 110 },
                new() { Header = "HSN Code",     BindingPath = nameof(BarcodeLabelRow.HSNCode),      Width = 90 },
                new() { Header = "Size",         BindingPath = nameof(BarcodeLabelRow.Size),         Width = 70 },
                new() { Header = "Colour",       BindingPath = nameof(BarcodeLabelRow.Colour),       Width = 80 },
                new() { Header = "Batch",        BindingPath = nameof(BarcodeLabelRow.Batch),        Width = 90 },
                new() { Header = "Mfg Date",     BindingPath = nameof(BarcodeLabelRow.MfgDate),      StringFormat = "dd-MM-yyyy", Width = 100 },
                new() { Header = "Exp Date",     BindingPath = nameof(BarcodeLabelRow.ExpDate),      StringFormat = "dd-MM-yyyy", Width = 100 },
                new() { Header = "Godown",       BindingPath = nameof(BarcodeLabelRow.Godown),       Width = 80 },
                new() { Header = "Rack",         BindingPath = nameof(BarcodeLabelRow.Rack),         Width = 70 },
                new() { Header = "CGST %",       BindingPath = nameof(BarcodeLabelRow.CGST),         StringFormat = "N1", Width = 70 },
                new() { Header = "SGST %",       BindingPath = nameof(BarcodeLabelRow.SGST),         StringFormat = "N1", Width = 70 },
                new() { Header = "IGST %",       BindingPath = nameof(BarcodeLabelRow.IGST),         StringFormat = "N1", Width = 70 },
            };
        }

        private void OnColumnPickerLoaded()
        {
            foreach (var opt in _columnOptions)
            {
                opt.CheckedChanged += (_, _) => ToggleOptionalColumn(opt);
                if (opt.IsChecked) AddOptionalColumn(opt);
            }
        }

        private void ToggleOptionalColumn(BarcodeColumnOption opt)
        {
            if (opt.IsChecked) AddOptionalColumn(opt);
            else RemoveOptionalColumn(opt);
        }

        private void AddOptionalColumn(BarcodeColumnOption opt)
        {
            if (_activeOptionalColumns.ContainsKey(opt)) return;

            var binding = new Binding(opt.BindingPath);
            if (!string.IsNullOrEmpty(opt.StringFormat))
                binding.StringFormat = "{0:" + opt.StringFormat + "}";

            var column = new DataGridTextColumn { Header = opt.Header, Binding = binding, Width = opt.Width };
            LabelsGrid.Columns.Add(column);
            _activeOptionalColumns[opt] = column;
        }

        private void RemoveOptionalColumn(BarcodeColumnOption opt)
        {
            if (_activeOptionalColumns.TryGetValue(opt, out var column))
            {
                LabelsGrid.Columns.Remove(column);
                _activeOptionalColumns.Remove(opt);
            }
        }

        // ── Custom templates ─────────────────────────────────────────────────
        private void LoadCustomTemplates()
        {
            foreach (var tpl in TemplateStorageService.LoadAll())
                TemplateList.Items.Add(new ListBoxItem { Content = tpl.Name, Tag = tpl });
        }

        private void NewTemplateButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new TemplateDesignerWindow { Owner = this };
            if (dlg.ShowDialog() == true && dlg.SavedTemplate != null)
                AddOrUpdateCustomTemplateItem(dlg.SavedTemplate);
        }

        private void EditTemplateButton_Click(object sender, RoutedEventArgs e)
        {
            if (TemplateList.SelectedItem is not ListBoxItem { Tag: LabelTemplate tpl })
            {
                MessageBox.Show(this, "Select a custom template from the list first.", "No template selected",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new TemplateDesignerWindow(tpl) { Owner = this };
            if (dlg.ShowDialog() == true && dlg.SavedTemplate != null)
                AddOrUpdateCustomTemplateItem(dlg.SavedTemplate);
        }

        private void AddOrUpdateCustomTemplateItem(LabelTemplate tpl)
        {
            var existing = TemplateList.Items.OfType<ListBoxItem>()
                .FirstOrDefault(i => i.Tag is LabelTemplate t && t.Name == tpl.Name);

            if (existing != null)
            {
                existing.Content = tpl.Name;
                existing.Tag = tpl;
                TemplateList.SelectedItem = existing;
            }
            else
            {
                var item = new ListBoxItem { Content = tpl.Name, Tag = tpl };
                TemplateList.Items.Add(item);
                TemplateList.SelectedItem = item;
            }
            UpdatePreviewVisibility();
        }

        // ── Template + preview ───────────────────────────────────────────────
        private void TemplateList_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdatePreviewVisibility();

        private void LabelsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdatePreviewVisibility();

        private void UpdatePreviewVisibility()
        {
            if (NoSelectionHint == null || StandardPreview == null || CompactPreview == null ||
                DetailedPreview == null || CustomTemplateViewbox == null)
                return;

            bool hasSelection = LabelsGrid.SelectedItem != null;
            NoSelectionHint.Visibility = hasSelection ? Visibility.Collapsed : Visibility.Visible;

            var selectedItem = TemplateList.SelectedItem as ListBoxItem;
            string? builtIn = selectedItem?.Tag as string;
            var customTemplate = selectedItem?.Tag as LabelTemplate;

            StandardPreview.Visibility = hasSelection && builtIn == "Standard" ? Visibility.Visible : Visibility.Collapsed;
            CompactPreview.Visibility = hasSelection && builtIn == "Compact" ? Visibility.Visible : Visibility.Collapsed;
            DetailedPreview.Visibility = hasSelection && builtIn == "Detailed" ? Visibility.Visible : Visibility.Collapsed;

            if (hasSelection && customTemplate != null)
            {
                CustomTemplatePreview.Content = TemplateRenderer.Render(customTemplate, LabelsGrid.SelectedItem);
                CustomTemplateViewbox.Visibility = Visibility.Visible;
            }
            else
            {
                CustomTemplateViewbox.Visibility = Visibility.Collapsed;
            }
        }

        // ── Print selected ───────────────────────────────────────────────────
        private void PrintSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            var rows = _rows.Where(r => r.IsSelected).ToList();

            if (rows.Count == 0)
            {
                MessageBox.Show(this, "Check the \"Print?\" box on at least one row before printing.",
                    "Nothing to print", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Same queue lookup as ProductLabelPrintWindow — saved default printer, or bail with a warning.
            var queue = PrinterSettingsService.GetDefaultPrintQueue();
            if (queue == null)
            {
                MessageBox.Show(this, "No printer is configured. Please set one in Printer Settings.",
                    "Printer Not Set", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedItem = TemplateList.SelectedItem as ListBoxItem;
            string builtIn = selectedItem?.Tag as string ?? "Standard";
            var customTemplate = selectedItem?.Tag as LabelTemplate;

            var pd = new PrintDialog { PrintQueue = queue };
            int printed = 0;

            foreach (var row in rows)
            {
                var (visual, widthPx, heightPx) = BuildPrintVisual(row, builtIn, customTemplate);

                pd.PrintTicket.PageMediaSize = new PageMediaSize(widthPx, heightPx);
                visual.Measure(new Size(widthPx, heightPx));
                visual.Arrange(new Rect(0, 0, widthPx, heightPx));
                visual.UpdateLayout();

                double copies = row.Quantity;
                if (copies < 1) copies = 1;

                for (int i = 0; i < copies; i++)
                {
                    pd.PrintVisual(visual, $"Barcode Label – {row.ProductName} [{row.Barcode}]");
                    printed++;
                }
            }

            MessageBox.Show(this, $"{printed} label(s) sent to \"{queue.FullName}\".",
                "Print", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private static (FrameworkElement Visual, double WidthPx, double HeightPx) BuildPrintVisual(
            BarcodeLabelRow row, string builtIn, LabelTemplate? customTemplate)
        {
            if (customTemplate != null)
            {
                var visual = TemplateRenderer.Render(customTemplate, row);
                double w = customTemplate.WidthMm * TemplateRenderer.PxPerMm;
                double h = customTemplate.HeightMm * TemplateRenderer.PxPerMm;
                return (visual, w, h);
            }

            FrameworkElement built = builtIn switch
            {
                "Compact" => BuildCompactVisual(row),
                "Detailed" => BuildDetailedVisual(row),
                _ => BuildStandardVisual(row),
            };

            // Built-in templates aren't tied to a physical mm size (unlike the designer's
            // custom templates), so print them at their natural content-driven size.
            built.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return (built, built.DesiredSize.Width, built.DesiredSize.Height);
        }

        private static readonly Brush StoreNameBrush = (Brush)new BrushConverter().ConvertFromString("#1F3A5F")!;
        private static readonly Brush MutedBrush = (Brush)new BrushConverter().ConvertFromString("#868E96")!;

        private static StackPanel BuildStandardVisual(BarcodeLabelRow row)
        {
            var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Width = 220 };

            panel.Children.Add(new TextBlock
            {
                Text = "My Store",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = StoreNameBrush,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            panel.Children.Add(new TextBlock
            {
                Text = row.ProductName,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 2, 0, 4)
            });
            panel.Children.Add(new Image { Source = row.BarcodeImage, Width = 200, Height = 55, Stretch = Stretch.Fill });
            panel.Children.Add(new TextBlock
            {
                Text = row.Barcode,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 4)
            });

            var priceRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            priceRow.Children.Add(new TextBlock { Text = "MRP: ₹", FontSize = 11, FontWeight = FontWeights.Bold });
            priceRow.Children.Add(new TextBlock { Text = row.MRP.ToString("N2"), FontSize = 11, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 10, 0) });
            priceRow.Children.Add(new TextBlock { Text = "Sale: ₹", FontSize = 10 });
            priceRow.Children.Add(new TextBlock { Text = row.Retail.ToString("N2"), FontSize = 10 });
            panel.Children.Add(priceRow);

            return panel;
        }

        private static StackPanel BuildCompactVisual(BarcodeLabelRow row)
        {
            var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Width = 220 };
            panel.Children.Add(new Image { Source = row.BarcodeImage, Width = 200, Height = 60, Stretch = Stretch.Fill });
            panel.Children.Add(new TextBlock
            {
                Text = row.Barcode,
                FontFamily = new FontFamily("Consolas"),
                FontWeight = FontWeights.SemiBold,
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 3, 0, 0)
            });
            return panel;
        }

        private static StackPanel BuildDetailedVisual(BarcodeLabelRow row)
        {
            var panel = BuildStandardVisual(row);

            var detailRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) };
            detailRow.Children.Add(new TextBlock { Text = "HSN: ", FontSize = 9, Foreground = MutedBrush });
            detailRow.Children.Add(new TextBlock { Text = row.HSNCode, FontSize = 9, Foreground = MutedBrush, Margin = new Thickness(0, 0, 8, 0) });
            detailRow.Children.Add(new TextBlock { Text = "Size: ", FontSize = 9, Foreground = MutedBrush });
            detailRow.Children.Add(new TextBlock { Text = row.Size, FontSize = 9, Foreground = MutedBrush, Margin = new Thickness(0, 0, 8, 0) });
            detailRow.Children.Add(new TextBlock { Text = "Colour: ", FontSize = 9, Foreground = MutedBrush });
            detailRow.Children.Add(new TextBlock { Text = row.Colour, FontSize = 9, Foreground = MutedBrush });
            panel.Children.Add(detailRow);

            return panel;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}