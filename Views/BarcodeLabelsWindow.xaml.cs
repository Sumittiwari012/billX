using MyWPFCRUDApp.Helpers;
using MyWPFCRUDApp.Models;
using MyWPFCRUDApp.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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

        // False while the window is still being built / saved values are being
        // applied, so those programmatic changes don't write the file back.
        private bool _settingsReady;

        public BarcodeLabelsWindow(IEnumerable<MPurchaseDetail> items, ProductService? productService = null)
        {
            InitializeComponent();
            _productService = productService ?? new ProductService();

            _rows = new ObservableCollection<BarcodeLabelRow>(
                items
                    .Where(i => !string.IsNullOrWhiteSpace(i.Barcode))
                    .Select(BuildRow));

            // Keep the "N selected" label live as individual "Print?" checkboxes
            // are toggled, not just when Select All/Unselect All are clicked.
            // This only wires up if BarcodeLabelRow implements
            // INotifyPropertyChanged (it should, since IsSelected is already a
            // two-way DataGrid binding) — if it doesn't, this loop is a no-op
            // and only the two bulk buttons will update the count.
            foreach (var row in _rows)
            {
                if (row is INotifyPropertyChanged notifying)
                {
                    notifying.PropertyChanged += (_, args) =>
                    {
                        if (args.PropertyName == nameof(BarcodeLabelRow.IsSelected))
                            UpdateSelectedCountText();
                        else if (args.PropertyName == nameof(BarcodeLabelRow.PrintQuantity) &&
                                 ReferenceEquals(LabelsGrid.SelectedItem, row))
                            UpdatePreviewVisibility();
                    };
                }
            }

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
            ApplySavedSettings();
            UpdatePreviewVisibility();
            UpdateSelectedCountText();
        }

        // ── Remembered print layout (labels across + gap + last template) ────
        // Persisted by LabelPrintSettingsService in %APPDATA%, which survives
        // clearing cache / temp files.
        private void ApplySavedSettings()
        {
            var saved = LabelPrintSettingsService.Load();

            LabelsAcrossBox.Text = Math.Clamp(saved.LabelsAcross, 1, MaxLabelsAcross).ToString();
            GapMmBox.Text = saved.GapMm.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

            if (!string.IsNullOrEmpty(saved.SelectedTemplateKey))
            {
                var match = TemplateList.Items.OfType<ListBoxItem>()
                    .FirstOrDefault(i => TemplateKey(i) == saved.SelectedTemplateKey);
                if (match != null) TemplateList.SelectedItem = match;
            }

            _settingsReady = true;
        }

        private static string? TemplateKey(ListBoxItem? item) => item?.Tag switch
        {
            string builtIn => "builtin:" + builtIn,
            LabelTemplate tpl => "custom:" + tpl.Name,
            _ => null
        };

        // Upper limit for the typed "Labels across" value.
        private const int MaxLabelsAcross = 20;

        private int GetLabelsAcross() =>
            int.TryParse((LabelsAcrossBox.Text ?? "").Trim(), out var n)
                ? Math.Clamp(n, 1, MaxLabelsAcross)
                : 1;

        // Lets only digits be typed into the Labels across box.
        private void DigitsOnly_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
            => e.Handled = !e.Text.All(char.IsDigit);

        private double GetGapMm()
        {
            var text = (GapMmBox.Text ?? "").Trim().Replace(',', '.');
            return double.TryParse(text, System.Globalization.NumberStyles.Float,
                       System.Globalization.CultureInfo.InvariantCulture, out var v)
                ? Math.Clamp(v, 0, 50)
                : 0;
        }

        private void SaveSettings()
        {
            if (!_settingsReady) return;

            LabelPrintSettingsService.Save(new LabelPrintSettings
            {
                LabelsAcross = GetLabelsAcross(),
                GapMm = GetGapMm(),
                SelectedTemplateKey = TemplateKey(TemplateList.SelectedItem as ListBoxItem)
            });
        }

        private void LabelsAcrossBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SaveSettings();
            if (_settingsReady) UpdatePreviewVisibility();
        }

        private void GapMmBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SaveSettings();
            if (_settingsReady) UpdatePreviewVisibility();
        }

        private BarcodeLabelRow BuildRow(MPurchaseDetail item)
        {
            var product = _productService.GetByBarcode(item.Barcode);
            return new BarcodeLabelRow
            {
                Barcode = item.Barcode,
                Quantity = item.Quantity,
                // Print Qty starts equal to the invoice quantity — the common
                // case is "print one label per unit purchased" — but it's a
                // separate, editable field so the user can dial it down (or up)
                // per row without touching the invoice record itself.
                PrintQuantity = item.Quantity,
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
                // CHANGED: MProducts.Rack is now long?. BarcodeLabelRow.Rack is
                // kept as a string (it's only displayed), so convert here.
                // If you change BarcodeLabelRow.Rack to long?, use: Rack = product?.Rack,
                Rack = product?.Rack?.ToString(),
                PartGroup = product?.PartGroup,
                Description = product?.Description,
                DiscountPercentage = product?.DiscountPercentage ?? 0,
                CGST = product?.CGST ?? (double)item.CGST,
                SGST = product?.SGST ?? (double)item.SGST,
                IGST = product?.IGST ?? (double)item.IGST,
                CESS = product?.CESS ?? 0,
                // Nothing pre-checked — the user opts in per row, or uses
                // "Select All" in the toolbar above the grid.
                IsSelected = false
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

            var column = new DataGridTextColumn { Header = opt.Header, Binding = binding, Width = opt.Width, IsReadOnly = true };
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

        // ── Select all / unselect all / reset print qty ────────────────────
        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var row in _rows) row.IsSelected = true;
            UpdateSelectedCountText();
        }

        private void UnselectAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var row in _rows) row.IsSelected = false;
            UpdateSelectedCountText();
        }

        // Restores Print Qty back to each row's original invoice Quantity,
        // undoing any manual per-row edits in one click.
        private void ResetPrintQuantityButton_Click(object sender, RoutedEventArgs e)
        {
            // Commit any pending in-progress cell edit first, so a value the
            // user is still typing doesn't clobber the reset a moment later.
            LabelsGrid.CommitEdit(DataGridEditingUnit.Cell, true);

            foreach (var row in _rows) row.PrintQuantity = row.Quantity;
        }

        private void UpdateSelectedCountText()
        {
            if (SelectedCountText == null) return; // not constructed yet
            int count = _rows.Count(r => r.IsSelected);
            SelectedCountText.Text = count == 0 ? "" : $"{count} selected";
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
        private void TemplateList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePreviewVisibility();
            SaveSettings();
        }

        private void LabelsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdatePreviewVisibility();

        // Shows the selected row exactly as it will print: the chosen template,
        // repeated "Labels across" times with the chosen gap, on one strip.
        // Uses the same BuildStrip/BuildPrintVisual as PrintSelectedButton_Click.
        private void UpdatePreviewVisibility()
        {
            if (NoSelectionHint == null || StripPreviewPanel == null || StripPreviewHost == null ||
                StripInfoText == null || LabelsAcrossBox == null || GapMmBox == null)
                return;

            var row = LabelsGrid.SelectedItem as BarcodeLabelRow;
            bool hasSelection = row != null;
            NoSelectionHint.Visibility = hasSelection ? Visibility.Collapsed : Visibility.Visible;
            StripPreviewPanel.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;

            if (row == null)
            {
                StripPreviewHost.Content = null;
                return;
            }

            var selectedItem = TemplateList.SelectedItem as ListBoxItem;
            string builtIn = selectedItem?.Tag as string ?? "Standard";
            var customTemplate = selectedItem?.Tag as LabelTemplate;

            int across = GetLabelsAcross();
            double gapPx = GetGapMm() * TemplateRenderer.PxPerMm;

            var (_, labelW, labelH) = BuildPrintVisual(row, builtIn, customTemplate);
            StripPreviewHost.Content = BuildStrip(row, builtIn, customTemplate, across, across, labelW, labelH, gapPx, preview: true);

            double stripWmm = (across * labelW + (across - 1) * gapPx) / TemplateRenderer.PxPerMm;
            double labelWmm = labelW / TemplateRenderer.PxPerMm;
            double labelHmm = labelH / TemplateRenderer.PxPerMm;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            StripInfoText.Text =
                $"{across} across · gap {GetGapMm().ToString("0.##", inv)} mm\n" +
                $"label {labelWmm.ToString("0.#", inv)} × {labelHmm.ToString("0.#", inv)} mm · " +
                $"strip {stripWmm.ToString("0.#", inv)} × {labelHmm.ToString("0.#", inv)} mm";

            // How Print Qty will be split into strips, e.g. 37 labels at 2 across
            // = 18 full strips + 1 strip with a single label.
            int copies = (int)Math.Round(row.PrintQuantity, MidpointRounding.AwayFromZero);
            if (copies > 0)
            {
                int full = copies / across;
                int rest = copies % across;
                var parts = new List<string>();
                if (full > 0) parts.Add($"{full} strip(s) of {across}");
                if (rest > 0) parts.Add($"1 strip of {rest}");
                StripInfoText.Text += $"\nPrint Qty {copies} → " + string.Join(" + ", parts) +
                                      $" = {full + (rest > 0 ? 1 : 0)} strip(s)";
            }
        }

        // ── Print selected ───────────────────────────────────────────────────
        private void PrintSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            // Commit any Print Qty cell the user is mid-edit on, so a value
            // that hasn't left the TextBox yet still gets used for this run.
            LabelsGrid.CommitEdit(DataGridEditingUnit.Cell, true);

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
            int strips = 0;

            int across = GetLabelsAcross();
            double gapPx = GetGapMm() * TemplateRenderer.PxPerMm;

            foreach (var row in rows)
            {
                // Print Qty = total number of labels wanted for this row.
                // With "labels across" > 1 they are laid out side by side on a
                // strip, so 20 labels at 2-across is 10 strips.
                int copies = (int)Math.Round(row.PrintQuantity, MidpointRounding.AwayFromZero);
                if (copies <= 0) continue;

                // Size of ONE label (custom templates: from mm; built-ins: content size).
                var (_, labelW, labelH) = BuildPrintVisual(row, builtIn, customTemplate);

                // Full strip width: every slot plus the gaps between them. Empty
                // slots on a partial last strip are left blank but still take
                // their space, so label positions always line up with the roll.
                double stripW = across * labelW + (across - 1) * gapPx;
                pd.PrintTicket.PageMediaSize = new PageMediaSize(stripW, labelH);

                int fullStrips = copies / across;
                int remainder = copies % across;

                foreach (var (slots, times) in new[] { (across, fullStrips), (remainder, 1) })
                {
                    if (slots <= 0 || times <= 0) continue;

                    var strip = BuildStrip(row, builtIn, customTemplate, slots, across, labelW, labelH, gapPx);

                    // Ask the driver what it will ACTUALLY give us at that page size.
                    // Many label/thermal printers have hardware margins (a printable
                    // area inset from the physical page edges). If we ignore that and
                    // just arrange our visual to fill (0,0)→(width,height), the driver
                    // either clips whatever falls outside its real printable rectangle
                    // or shifts our content into it — which is what was pushing
                    // prints to the left and clipping the left edge.
                    var pageVisual = WrapForImageableArea(strip, queue, pd.PrintTicket, stripW, labelH);

                    pageVisual.Measure(new Size(stripW, labelH));
                    pageVisual.Arrange(new Rect(0, 0, stripW, labelH));
                    pageVisual.UpdateLayout();

                    for (int i = 0; i < times; i++)
                    {
                        pd.PrintVisual(pageVisual, $"Barcode Label – {row.ProductName} [{row.Barcode}]");
                        printed += slots;
                        strips++;
                    }
                }
            }

            if (printed == 0)
            {
                MessageBox.Show(this, "All selected rows have a Print Qty of 0 — nothing was sent to the printer.",
                    "Nothing printed", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            MessageBox.Show(this, $"{printed} label(s) sent to \"{queue.FullName}\" ({strips} strip(s), up to {across} across).",
                "Print", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Wraps the label content in a container the size of the physical page
        // and offsets it by the printer's reported imageable-area origin, so
        // the content lands inside the driver's real printable rectangle
        // instead of being clipped/shifted by hardware margins. Falls back to
        // the visual as-is if the driver can't report capabilities.
        private static FrameworkElement WrapForImageableArea(
            FrameworkElement visual, PrintQueue queue, PrintTicket ticket, double pageWidth, double pageHeight)
        {
            PrintCapabilities? capabilities = null;
            try
            {
                capabilities = queue.GetPrintCapabilities(ticket);
            }
            catch
            {
                // Some drivers throw or return incomplete capabilities — just
                // print the visual unwrapped rather than failing the job.
                return visual;
            }

            var imageable = capabilities?.PageImageableArea;
            if (imageable == null)
                return visual;

            var container = new Grid { Width = pageWidth, Height = pageHeight };
            visual.HorizontalAlignment = HorizontalAlignment.Left;
            visual.VerticalAlignment = VerticalAlignment.Top;
            visual.Margin = new Thickness(imageable.OriginWidth, imageable.OriginHeight, 0, 0);
            container.Children.Add(visual);
            return container;
        }

        // One printed strip: `slots` identical labels side by side, each label
        // in its own fixed-size cell, separated by gapPx. Every slot gets its
        // own freshly built visual because a WPF element can only have one parent.
        private static FrameworkElement BuildStrip(
            BarcodeLabelRow row, string builtIn, LabelTemplate? customTemplate,
            int slots, int across, double labelW, double labelH, double gapPx, bool preview = false)
        {
            var canvas = new Canvas
            {
                Width = across * labelW + (across - 1) * gapPx,
                Height = labelH
            };

            // Preview only: grey strip behind white labels so the gaps are visible.
            // Never set when printing, so printed output is unchanged.
            if (preview) canvas.Background = new SolidColorBrush(Color.FromRgb(0xCE, 0xD4, 0xDA));

            for (int i = 0; i < slots; i++)
            {
                var (visual, _, _) = BuildPrintVisual(row, builtIn, customTemplate);
                var cell = new Grid { Width = labelW, Height = labelH };
                if (preview) cell.Background = Brushes.White;
                cell.Children.Add(visual);
                Canvas.SetLeft(cell, i * (labelW + gapPx));
                Canvas.SetTop(cell, 0);
                canvas.Children.Add(cell);
            }

            return canvas;
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
            var standardBarcodeImg = new Image { Source = row.BarcodeImage, Width = 200, Height = 55, Stretch = Stretch.Fill };
            // Same reasoning as TemplateRenderer: this is a downscale (native
            // bitmap is 600x180), and HighQuality resamples correctly on
            // downscale where NearestNeighbor would drop/merge bar columns.
            RenderOptions.SetBitmapScalingMode(standardBarcodeImg, BitmapScalingMode.HighQuality);
            panel.Children.Add(standardBarcodeImg);
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
            var compactBarcodeImg = new Image { Source = row.BarcodeImage, Width = 200, Height = 60, Stretch = Stretch.Fill };
            RenderOptions.SetBitmapScalingMode(compactBarcodeImg, BitmapScalingMode.HighQuality);
            panel.Children.Add(compactBarcodeImg);
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