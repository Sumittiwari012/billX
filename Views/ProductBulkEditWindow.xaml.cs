using MyWPFCRUDApp.Models;
using MyWPFCRUDApp.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MyWPFCRUDApp.Views
{
    // ────────────────────────────────────────────────────────────────────────
    // One row in the bulk-edit grid. Wraps a real MProducts instance (for
    // existing products, the SAME instance that will be sent to UpdateProduct;
    // for brand-new/added-by-variety products, a fresh MProducts with
    // ProductId == 0-equivalent, i.e. IsNew == true, sent to InsertProduct
    // on Save).
    // ────────────────────────────────────────────────────────────────────────
    public class ProductEditRow : INotifyPropertyChanged
    {
        public MProducts Product { get; set; }
        public bool IsNew { get; set; }

        public MPurchaseDetail? SourceInvoiceItem { get; set; }

        public string StatusLabel => IsNew ? "NEW" : "EXISTING";

        // ← add it here
        private double _quantity;
        public double Quantity
        {
            get => SourceInvoiceItem?.Quantity ?? _quantity;
            set
            {
                if (SourceInvoiceItem != null)
                {
                    if (SourceInvoiceItem.Quantity != value)
                    {
                        SourceInvoiceItem.Quantity = value; // recalculates AfterTaxation
                        OnPropertyChanged();
                    }
                }
                else
                {
                    if (_quantity != value)
                    {
                        _quantity = value;
                        OnPropertyChanged();
                    }
                }
            }
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
        }

        // Display-only lookups, refreshed whenever CategoryId/SubCategoryId/
        // UnitId change (via RefreshLookupNames, called after any edit).
        public string CategoryName { get; set; } = "";
        public string SubCategoryName { get; set; } = "";
        public string UnitName { get; set; } = "";

        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
    // Column-picker checkbox item
    public class ColumnOption : INotifyPropertyChanged
    {
        public string Header { get; set; } = "";

        private bool _isVisible;
        public bool IsVisible
        {
            get => _isVisible;
            set { if (_isVisible != value) { _isVisible = value; OnPropertyChanged(); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class ProductBulkEditWindow : Window
    {
        private readonly ProductService _productService = new ProductService();
        private readonly CategoryService _categoryService = new CategoryService();
        private readonly SubCategoryService _subCategoryService = new SubCategoryService();
        private readonly UnitService _unitService = new UnitService();

        public ObservableCollection<ProductEditRow> Rows { get; } = new();
        public List<MCategory> Categories { get; private set; } = new();
        public List<MSubCategory> SubCategories { get; private set; } = new();
        public List<MUnit> Units { get; private set; } = new();
        public List<BulkEditResultLine> ResultLines { get; private set; } = new();

        // Columns that can never be hidden — not offered in the picker.
        private static readonly HashSet<string> AlwaysVisible = new()
{
    "ProductName"
};

        // ── Barcode parsing for "Add Copies" — splits a barcode into its
        //    non-numeric prefix and trailing numeric suffix, e.g. "GR78450"
        //    -> ("GR", 78450), so copies can be numbered relative to the row
        //    they were copied from instead of a global running counter. ──
        private (string prefix, long number) ParseBarcode(string? barcode)
        {
            var match = Regex.Match(barcode ?? "", @"^(.*?)(\d+)$");
            if (match.Success)
                return (match.Groups[1].Value, long.Parse(match.Groups[2].Value));

            // No trailing digits at all (rare) — treat the whole string as
            // the prefix so copies still land in the same "family".
            return (barcode ?? "", 0);
        }

        // True if `barcode` already belongs to some other product — either
        // a row already in this grid (other than `exclude`) or an existing
        // DB product — so shifted/new barcodes from Add Copies never
        // collide with something else.
        private bool BarcodeTakenElsewhere(string barcode, MProducts? exclude)
        {
            if (Rows.Any(r => r.Product != exclude && r.Product.Barcode == barcode))
                return true;

            var dbMatch = _productService.GetByBarcode(barcode);
            if (dbMatch == null) return false;
            return exclude == null || dbMatch.Id != exclude.Id;
        }

        // Result handed back to PurchaseViewModel after a successful Save.
        public List<MProducts> SavedProducts { get; private set; } = new();

        // Rows with NO SourceInvoiceItem — i.e. created fresh in this session
        // via "Add Copies". These are genuinely new invoice lines. This is now
        // narrower than "every IsNew row": a not-yet-saved product that was
        // already on the invoice is IsNew too, but it has a SourceInvoiceItem,
        // so it's reported via UpdatedInvoiceLines instead — otherwise it would
        // get added as a second, duplicate invoice line.
        //
        // FIX: previously List<MProducts> — the quantity typed into the grid
        // for these brand-new rows (which lives on ProductEditRow._quantity,
        // since there's no SourceInvoiceItem to hold it) was being dropped
        // entirely, so the caller had no choice but to default Quantity to 1
        // when building the new MPurchaseDetail. Now each entry carries its
        // row's Quantity alongside the Product.
        public List<(MProducts Product, double Quantity)> NewProducts { get; private set; } = new();

        // Direct row -> invoice-line links, for every row that already had a
        // corresponding PurchaseItems entry (existing DB products AND
        // not-yet-saved products that were already on the invoice). The caller
        // updates these invoice lines by reference — not by matching Barcode,
        // since Add Copies can renumber a row's barcode even if the row itself
        // was never touched.
        public List<(MPurchaseDetail Source, MProducts Product)> UpdatedInvoiceLines { get; private set; } = new();

        public List<string> DeletedBarcodes { get; private set; } = new();

        // ── Staged product-master changes — NOTHING here is written to the
        //    database by this window. Bulk Edit's job is purely to shape the
        //    in-memory rows for this invoice session (bulk field edits, Add
        //    Copies for variance, deletions). The caller (PurchaseViewModel)
        //    is responsible for actually calling Insert/Update/DeleteProduct,
        //    and only does so when the user clicks SAVE INVOICE — so closing
        //    this dialog, or even cancelling the whole invoice afterwards,
        //    has zero DB side effects. ──
        public List<MProducts> ProductsToInsert { get; private set; } = new(); // rows with IsNew == true
        public List<MProducts> ProductsToUpdate { get; private set; } = new(); // rows with IsNew == false, edited here
        public List<MProducts> ProductsToDelete { get; private set; } = new(); // from _pendingDeletes

        // Rows removed via "Delete Selected" are taken out of the grid right
        // away, but the actual DB delete (for pre-existing products) is
        // deferred until Save — so Cancel still discards everything,
        // including deletes, with zero DB side effects.
        private readonly List<ProductEditRow> _pendingDeletes = new();

        // ── Drag-select state for the ✓ column ──────────────────────────────
        private bool _isDragSelecting = false;
        private bool _dragSelectValue = false;
        private readonly HashSet<ProductEditRow> _dragTouchedRows = new();

        // Guards against re-entrant/rebound updates while we're setting
        // ChkSelectAll.IsChecked programmatically from UpdateSelectAllCheckboxState.
        private bool _suppressSelectAllClick = false;

        public ProductBulkEditWindow(List<MPurchaseDetail> invoiceItems)
        {
            InitializeComponent();
            DataContext = this;

            LoadLookups();
            BuildColumnPicker();
            BuildRows(invoiceItems);
            BuildBulkFieldOptions();

            ProductGrid.ItemsSource = Rows;

            // Intercept the row select-checkbox's mouse-down HERE, one level
            // up at the DataGrid, registered with handledEventsToo:true. This
            // runs first during the tunnel phase, before DataGridCell's own
            // built-in "first click just selects the cell" handling gets a
            // chance to swallow it — which is why a plain per-checkbox
            // PreviewMouseLeftButtonDown handler wasn't firing at all. This
            // single handler both toggles a single click AND kicks off a
            // drag-select gesture from the same mouse-down.
            ProductGrid.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent,
                new MouseButtonEventHandler(ProductGrid_RowCheckBoxPreviewMouseLeftButtonDown),
                true);

            // Keep the header checkbox (checked/unchecked/indeterminate) and
            // any future row additions/removals wired up to selection state.
            Rows.CollectionChanged += Rows_CollectionChanged;
            foreach (var r in Rows) r.PropertyChanged += Row_PropertyChanged;
            UpdateSelectAllCheckboxState();
        }

        // ── Lookups (Category/SubCategory/Unit) ─────────────────────────────
        private void LoadLookups()
        {
            Categories = _categoryService.GetCategory();
            SubCategories = _subCategoryService.GetSubCategoryList();
            Units = _unitService.GetUnit();
        }

        private string CategoryNameFor(long id) =>
            Categories.FirstOrDefault(c => c.Id == id)?.CategoryName ?? "—";
        private string SubCategoryNameFor(long id) =>
            SubCategories.FirstOrDefault(s => s.Id == id)?.SubCategoryName ?? "—";
        private string UnitNameFor(long id) =>
            Units.FirstOrDefault(u => u.Id == id)?.UnitName ?? "—";

        // ── Column picker ────────────────────────────────────────────────────
        private void BuildColumnPicker()
        {
            var options = new List<ColumnOption>();
            foreach (var col in ProductGrid.Columns)
            {
                // The ✓/select column is a DataGridTemplateColumn whose Header
                // is now a CheckBox control (for select-all), not the string
                // "✓" — compare by reference against the named column so it's
                // still recognized and skipped here regardless of header content.
                if (ReferenceEquals(col, SelectColumn))
                    continue;

                string header = col.Header?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(header) ||
                    AlwaysVisible.Contains(header) || header == "Barcode")
                    continue;

                options.Add(new ColumnOption
                {
                    Header = header,
                    IsVisible = col.Visibility == Visibility.Visible
                });
            }
            ColumnPickerItems.ItemsSource = options;
        }

        private void ColumnCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox cb || cb.Content is not string header) return;
            var col = ProductGrid.Columns.FirstOrDefault(c => (c.Header?.ToString() ?? "") == header);
            if (col != null)
                col.Visibility = (cb.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
        }

        // ── Build rows from the invoice's current items ─────────────────────
        private void BuildRows(List<MPurchaseDetail> invoiceItems)
        {
            var allProducts = _productService.GetProducts();

            foreach (var item in invoiceItems)
            {
                if (item.ProductId > 0)
                {
                    var existing = allProducts.FirstOrDefault(p => p.Id == item.ProductId)
                                   ?? _productService.GetByBarcode(item.Barcode);
                    if (existing != null)
                    {
                        AddRow(existing, isNew: false, sourceInvoiceItem: item);
                        continue;
                    }
                }

                // Not saved yet (e.g. from Excel import / scanned bill) —
                // build a skeleton MProducts from what the invoice line has.
                var skeleton = new MProducts
                {
                    ProductName = item.ProductName,
                    Barcode = item.Barcode,
                    CategoryId = Categories.Any() ? Categories.First().Id : 1,
                    SubCategoryId = SubCategories.Any() ? SubCategories.First().Id : 1,
                    UnitId = Units.Any() ? Units.First().Id : 1,
                    PurchasePrice = item.PurchasePrice,
                    WholesalePrice = item.WholesalePrice,
                    RetailSalePrice = item.Retail,
                    MRP = item.MRP,
                    HSNCode = item.HSNCode,
                    Size = item.Size,
                    Colour = item.Colour,
                    CGST = (double)item.CGST,
                    SGST = (double)item.SGST,
                    IGST = (double)item.IGST,
                    CESS = 0
                };
                AddRow(skeleton, isNew: true, sourceInvoiceItem: item);
            }
        }

        // insertIndex == null -> append at the end (used when first building
        // the grid from the invoice). Otherwise the row is inserted at that
        // exact position — used by Add Copies so a copy lands directly under
        // the row it was copied from rather than at the bottom of the list.
        private ProductEditRow AddRow(MProducts product, bool isNew, int? insertIndex = null,
            MPurchaseDetail? sourceInvoiceItem = null)
        {
            var row = new ProductEditRow
            {
                Product = product,
                IsNew = isNew,
                SourceInvoiceItem = sourceInvoiceItem,
                CategoryName = CategoryNameFor(product.CategoryId),
                SubCategoryName = SubCategoryNameFor(product.SubCategoryId),
                UnitName = UnitNameFor(product.UnitId)
            };

            if (insertIndex.HasValue)
                Rows.Insert(insertIndex.Value, row);
            else
                Rows.Add(row);

            return row;
        }

        private void RefreshLookupDisplay(ProductEditRow row)
        {
            row.CategoryName = CategoryNameFor(row.Product.CategoryId);
            row.SubCategoryName = SubCategoryNameFor(row.Product.SubCategoryId);
            row.UnitName = UnitNameFor(row.Product.UnitId);
            row.OnPropertyChanged(nameof(row.CategoryName));
            row.OnPropertyChanged(nameof(row.SubCategoryName));
            row.OnPropertyChanged(nameof(row.UnitName));
        }

        // ══════════════════════════════════════════════════════════════════
        // SELECT ALL / UNSELECT ALL + CLICK-AND-DRAG MULTI-SELECT
        // ══════════════════════════════════════════════════════════════════

        // Toolbar "Select All" button.
        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var row in Rows) row.IsSelected = true;
        }

        // Toolbar "Unselect All" button.
        private void UnselectAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var row in Rows) row.IsSelected = false;
        }

        // Header checkbox above the ✓ column. Tri-state: checked -> select
        // all, unchecked -> unselect all. When the box was showing the
        // indeterminate dash (mixed selection) and the user clicks it, WPF
        // moves a ThreeState CheckBox from indeterminate to checked, which
        // we treat the same as "select all".
        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            if (_suppressSelectAllClick) return;

            bool newState = ChkSelectAll.IsChecked == true;
            foreach (var row in Rows) row.IsSelected = newState;
        }

        // Mouse-down anywhere under the DataGrid, filtered down to just the
        // row select-checkbox (walks up from whatever was actually clicked
        // to find the nearest CheckBox, then checks it's bound to a row).
        // Registered with handledEventsToo:true in the constructor so this
        // fires even though DataGridCell's own click handling would
        // otherwise mark the event handled first. Starts a drag-select
        // gesture: the value painted onto every row touched during the drag
        // is the OPPOSITE of whatever this first row currently is — i.e.
        // what it's about to become. A plain click with no movement just
        // toggles that one row.
        private void ProductGrid_RowCheckBoxPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var checkBox = FindAncestor<CheckBox>(e.OriginalSource as DependencyObject);
            if (checkBox?.DataContext is not ProductEditRow row) return; // not the select checkbox

            _isDragSelecting = true;
            _dragSelectValue = !row.IsSelected;
            _dragTouchedRows.Clear();
            _dragTouchedRows.Add(row);

            row.IsSelected = _dragSelectValue;

            ProductGrid.CaptureMouse();
            e.Handled = true;
        }

        // While dragging with the left button held, hit-test whatever row is
        // under the cursor and paint it to _dragSelectValue. Each row is only
        // touched once per drag (HashSet) so re-crossing it doesn't flicker
        // it back and forth.
        private void ProductGrid_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragSelecting || e.LeftButton != MouseButtonState.Pressed) return;

            var point = e.GetPosition(ProductGrid);
            var hit = VisualTreeHelper.HitTest(ProductGrid, point);
            if (hit == null) return;

            var dataGridRow = FindAncestor<DataGridRow>(hit.VisualHit);
            if (dataGridRow?.Item is ProductEditRow editRow && _dragTouchedRows.Add(editRow))
                editRow.IsSelected = _dragSelectValue;
        }

        private void ProductGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDragSelecting) return;
            _isDragSelecting = false;
            ProductGrid.ReleaseMouseCapture();
        }

        private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T t) return t;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        // Keeps the header checkbox in sync: checked if every row is
        // selected, unchecked if none are, indeterminate (dash) if it's a
        // mix. Suppresses the Click handler while we set IsChecked here so
        // this programmatic update never re-triggers SelectAll_Click.
        private void UpdateSelectAllCheckboxState()
        {
            if (ChkSelectAll == null) return;

            _suppressSelectAllClick = true;
            try
            {
                if (!Rows.Any()) ChkSelectAll.IsChecked = false;
                else if (Rows.All(r => r.IsSelected)) ChkSelectAll.IsChecked = true;
                else if (Rows.All(r => !r.IsSelected)) ChkSelectAll.IsChecked = false;
                else ChkSelectAll.IsChecked = null;
            }
            finally
            {
                _suppressSelectAllClick = false;
            }
        }

        // Fires on every row's IsSelected change (drag-select, single click,
        // or bulk field edits that indirectly touch selection) so the header
        // checkbox stays accurate no matter how selection changed.
        private void Row_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProductEditRow.IsSelected))
                UpdateSelectAllCheckboxState();
        }

        // Keeps row PropertyChanged subscriptions and the header checkbox
        // correct as rows are added (Add Copies) or removed (Delete
        // Selected) after the window has already loaded.
        private void Rows_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (ProductEditRow r in e.NewItems)
                    r.PropertyChanged += Row_PropertyChanged;

            if (e.OldItems != null)
                foreach (ProductEditRow r in e.OldItems)
                    r.PropertyChanged -= Row_PropertyChanged;

            UpdateSelectAllCheckboxState();
        }

        // ── Bulk apply: "Full dropdown of any MProducts field" ──────────────
        // Reflection-driven so every settable field on MProducts is available,
        // not just a hardcoded shortlist. CGST/SGST/IGST are pulled OUT of
        // that reflected list and replaced with one clubbed virtual option —
        // setting a tax rate almost always means all three should match.
        private const string TaxRateVirtualField = "Tax Rate (CGST + SGST + IGST)";

        private static readonly string[] ExcludedFromBulk =
        {
            "Id", "Barcode", "ProductName", "createdBy", "createdDate", "modifiedBy", "modifiedDate",
            "CGST", "SGST", "IGST"
        };

        private void BuildBulkFieldOptions()
        {
            var props = typeof(MProducts).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite && !ExcludedFromBulk.Contains(p.Name))
                .Select(p => p.Name)
                .OrderBy(n => n)
                .ToList();

            props.Insert(0, TaxRateVirtualField);
            ComboBulkField.ItemsSource = props;
        }

        private void ComboBulkField_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string? field = ComboBulkField.SelectedItem as string;

            if (field == TaxRateVirtualField)
            {
                SetBulkValueMode(dropdown: false, null, null, taxRates: true);
            }
            else if (field == "CategoryId")
            {
                ComboBulkValue.ItemsSource = Categories;
                SetBulkValueMode(dropdown: true, "CategoryName", "Id");
            }
            else if (field == "SubCategoryId")
            {
                ComboBulkValue.ItemsSource = SubCategories;
                SetBulkValueMode(dropdown: true, "SubCategoryName", "Id");
            }
            else if (field == "UnitId")
            {
                ComboBulkValue.ItemsSource = Units;
                SetBulkValueMode(dropdown: true, "UnitName", "Id");
            }
            else
            {
                SetBulkValueMode(dropdown: false, null, null);
            }
        }

        private void SetBulkValueMode(bool dropdown, string? displayPath, string? valuePath, bool taxRates = false)
        {
            ComboBulkValue.DisplayMemberPath = displayPath ?? "";
            ComboBulkValue.SelectedValuePath = valuePath ?? "";
            ComboBulkValue.Visibility = dropdown ? Visibility.Visible : Visibility.Collapsed;
            TxtBulkValue.Visibility = (!dropdown && !taxRates) ? Visibility.Visible : Visibility.Collapsed;
            PanelBulkTaxRates.Visibility = taxRates ? Visibility.Visible : Visibility.Collapsed;
        }

        // Blank -> valid "no change" (returns null). Non-blank -> must parse
        // as a number, else shows an error naming the offending field and
        // returns false so the caller can bail out before applying anything.
        private bool TryParseOptionalRate(string text, string fieldLabel, out double? value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(text)) return true;

            if (!double.TryParse(text, out double parsed))
            {
                MessageBox.Show($"'{text}' isn't a valid {fieldLabel} percentage (e.g. 9).",
                    "Invalid Value", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            value = parsed;
            return true;
        }

        private void ApplyBulk_Click(object sender, RoutedEventArgs e)
        {
            string? fieldName = ComboBulkField.SelectedItem as string;
            if (string.IsNullOrEmpty(fieldName))
            {
                MessageBox.Show("Pick a field to apply first.", "No Field Selected",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var selectedRows = Rows.Where(r => r.IsSelected).ToList();
            if (!selectedRows.Any())
            {
                MessageBox.Show("Check at least one row (✓ column) first.", "No Rows Selected",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // ── Clubbed tax rate option: still one Apply click, but each of
            //    CGST/SGST/IGST has its own input now. A blank box means
            //    "leave that tax as-is" — you're not forced to set all three
            //    every time (e.g. only IGST changed for an inter-state item). ──
            if (fieldName == TaxRateVirtualField)
            {
                if (!TryParseOptionalRate(TxtBulkCGST.Text, "CGST", out double? cgst) ||
                    !TryParseOptionalRate(TxtBulkSGST.Text, "SGST", out double? sgst) ||
                    !TryParseOptionalRate(TxtBulkIGST.Text, "IGST", out double? igst))
                {
                    return; // TryParseOptionalRate already showed the error
                }

                if (cgst == null && sgst == null && igst == null)
                {
                    MessageBox.Show("Enter a value in at least one of CGST / SGST / IGST.",
                        "No Value", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                foreach (var row in selectedRows)
                {
                    if (cgst.HasValue) row.Product.CGST = cgst.Value;
                    if (sgst.HasValue) row.Product.SGST = sgst.Value;
                    if (igst.HasValue) row.Product.IGST = igst.Value;
                }

                var applied = new System.Collections.Generic.List<string>();
                if (cgst.HasValue) applied.Add($"CGST = {cgst.Value}%");
                if (sgst.HasValue) applied.Add($"SGST = {sgst.Value}%");
                if (igst.HasValue) applied.Add($"IGST = {igst.Value}%");

                ProductGrid.Items.Refresh();
                MessageBox.Show(
                    $"Applied {string.Join(", ", applied)} to {selectedRows.Count} product(s).",
                    "Bulk Update Applied", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var prop = typeof(MProducts).GetProperty(fieldName);
            if (prop == null) return;

            object? rawValue = ComboBulkValue.Visibility == Visibility.Visible
                ? ComboBulkValue.SelectedValue
                : TxtBulkValue.Text;

            if (rawValue == null || (rawValue is string s0 && string.IsNullOrWhiteSpace(s0)))
            {
                MessageBox.Show("Enter or pick a value first.", "No Value",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            object? converted;
            try
            {
                var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

                if (targetType == typeof(DateTime))
                    converted = DateTime.Parse(rawValue.ToString()!);
                else if (rawValue.GetType() == targetType)
                    converted = rawValue;
                else
                    converted = Convert.ChangeType(rawValue, targetType);
            }
            catch
            {
                MessageBox.Show(
                    $"'{rawValue}' isn't a valid value for {fieldName} " +
                    $"(expects {prop.PropertyType.Name}).",
                    "Invalid Value", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            foreach (var row in selectedRows)
            {
                prop.SetValue(row.Product, converted);
                if (fieldName == "CategoryId" || fieldName == "SubCategoryId" || fieldName == "UnitId")
                    RefreshLookupDisplay(row);
            }

            ProductGrid.Items.Refresh();

            MessageBox.Show($"Applied {fieldName} = {rawValue} to {selectedRows.Count} product(s).",
                "Bulk Update Applied", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ── Add Copies (variety): blank Size/Colour, everything else cloned
        //    from the base row. Copies are inserted directly under the row
        //    they were copied from — NOT appended at the bottom of the grid,
        //    which was hard to spot in a long list. To keep barcodes unique
        //    and sequential, anything already sitting in the numbers the new
        //    copies need gets pushed forward by `count`, e.g. base row is
        //    M101 and you add 2 copies: the copies become M102/M103, and
        //    whatever used to be M102/M103/... becomes M104/M105/... ──
        private void AddCopies_Click(object sender, RoutedEventArgs e)
        {
            var selectedRows = Rows.Where(r => r.IsSelected).ToList();
            if (!selectedRows.Any())
            {
                MessageBox.Show("Check the row(s) you want to create copies of first (✓ column).",
                    "No Rows Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!int.TryParse(TxtCopyCount.Text, out int count) || count < 1)
            {
                MessageBox.Show("Enter a valid number of copies (1 or more).",
                    "Invalid Count", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int totalAdded = 0;

            // selectedRows is already in on-screen (top-to-bottom) order.
            // Each iteration re-reads the row's live position/barcode via
            // Rows.IndexOf / ParseBarcode, so earlier insertions/shifts in
            // this same click are automatically accounted for.
            foreach (var baseRow in selectedRows)
            {
                int baseIndex = Rows.IndexOf(baseRow);
                var (prefix, baseNum) = ParseBarcode(baseRow.Product.Barcode);

                // Make room: push every other row sharing this barcode
                // "family" whose number is >= the first slot the copies
                // need, forward by `count`, so the copies can drop straight
                // in without colliding with what's already there.
                foreach (var other in Rows)
                {
                    if (other == baseRow) continue;
                    var (otherPrefix, otherNum) = ParseBarcode(other.Product.Barcode);
                    if (otherPrefix != prefix || otherNum < baseNum + 1) continue;

                    long shifted = otherNum + count;
                    while (BarcodeTakenElsewhere($"{prefix}{shifted}", other.Product))
                        shifted++;

                    other.Product.Barcode = $"{prefix}{shifted}";
                }

                for (int i = 0; i < count; i++)
                {
                    long newNum = baseNum + 1 + i;
                    string newBarcode = $"{prefix}{newNum}";
                    while (BarcodeTakenElsewhere(newBarcode, null))
                    {
                        newNum++;
                        newBarcode = $"{prefix}{newNum}";
                    }

                    var clone = new MProducts
                    {
                        ProductName = baseRow.Product.ProductName,
                        ProductCode = baseRow.Product.ProductCode,
                        Barcode = newBarcode,
                        // Guard against cloning a 0/invalid lookup id (e.g. a base
                        // row whose Product came from a code path that didn't
                        // populate these) — falling back to the first available
                        // lookup avoids an FK-constraint failure on Save.
                        CategoryId = baseRow.Product.CategoryId > 0
                            ? baseRow.Product.CategoryId
                            : (Categories.Any() ? Categories.First().Id : 1),
                        SubCategoryId = baseRow.Product.SubCategoryId > 0
                            ? baseRow.Product.SubCategoryId
                            : (SubCategories.Any() ? SubCategories.First().Id : 1),
                        UnitId = baseRow.Product.UnitId > 0
                            ? baseRow.Product.UnitId
                            : (Units.Any() ? Units.First().Id : 1),
                        HSNCode = baseRow.Product.HSNCode,
                        PartGroup = baseRow.Product.PartGroup,
                        Description = baseRow.Product.Description,
                        PurchasePrice = baseRow.Product.PurchasePrice,
                        RetailSalePrice = baseRow.Product.RetailSalePrice,
                        WholesalePrice = baseRow.Product.WholesalePrice,
                        DiscountPercentage = baseRow.Product.DiscountPercentage,
                        CGST = baseRow.Product.CGST,
                        SGST = baseRow.Product.SGST,
                        IGST = baseRow.Product.IGST,
                        CESS = baseRow.Product.CESS,
                        MRP = baseRow.Product.MRP,
                        Godown = baseRow.Product.Godown,
                        Rack = baseRow.Product.Rack,
                        Batch = baseRow.Product.Batch,
                        MfgDate = baseRow.Product.MfgDate,
                        ExpDate = baseRow.Product.ExpDate,
                        // Blank — filled in later, per "fixed count" variety mode
                        Size = null,
                        Colour = null,
                        IMEI1 = null,
                        IMEI2 = null
                    };

                    AddRow(clone, isNew: true, insertIndex: baseIndex + 1 + i);
                    totalAdded++;
                }
            }

            ProductGrid.Items.Refresh();
            MessageBox.Show(
                $"✔ Added {totalAdded} new copy/copies directly below the row(s) you copied. " +
                "Barcodes of any items that were in the way were renumbered to keep the list " +
                "in order.\nFill in Size/Colour directly in the grid if needed.",
                "Copies Added", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ── Delete Selected — removes rows from the grid immediately;
        //    for pre-existing products, the actual DB delete happens at
        //    Save (see Save_Click), so Cancel undoes this cleanly. ──
        private void DeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            var selectedRows = Rows.Where(r => r.IsSelected).ToList();
            if (!selectedRows.Any())
            {
                MessageBox.Show("Check the row(s) you want to delete first (✓ column).",
                    "No Rows Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int existingCount = selectedRows.Count(r => !r.IsNew);
            string warning = existingCount > 0
                ? $"{existingCount} of the {selectedRows.Count} selected product(s) already exist " +
                  "in your product master. They'll be permanently deleted from there (not just this " +
                  "invoice) once you click SAVE INVOICE on the Purchase screen — this can't be undone.\n\n" +
                  "Continue removing?"
                : $"Remove {selectedRows.Count} unsaved row(s) from this session?";

            var confirm = MessageBox.Show(warning, "Confirm Delete",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            foreach (var row in selectedRows)
            {
                if (!row.IsNew)
                    _pendingDeletes.Add(row);   // actual DB delete deferred to SAVE INVOICE

                DeletedBarcodes.Add(row.Product.Barcode);
                Rows.Remove(row);
            }

            MessageBox.Show(
                $"Removed {selectedRows.Count} product(s) from this list.\n" +
                (existingCount > 0 ? "Existing product(s) will be deleted from the database when you click SAVE INVOICE."
                                    : ""),
                "Removed", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ── Save ─────────────────────────────────────────────────────────────
        // Bulk Edit's sole purpose is shaping the in-memory rows for this
        // invoice session — bulk field edits, Add Copies (variance), and
        // deletions. It NEVER writes to the database itself. This method just
        // packages the final grid state for the caller; the actual
        // Insert/Update/Delete calls happen later, in PurchaseViewModel's
        // SavePurchase(), when the user clicks SAVE INVOICE on the Purchase
        // screen — so Cancel here, or cancelling the whole invoice afterwards,
        // always leaves the database untouched.
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var toInsert = Rows.Where(r => r.IsNew).ToList();
            var toUpdate = Rows.Where(r => !r.IsNew).ToList();

            foreach (var row in toInsert)
            {
                if (string.IsNullOrWhiteSpace(row.Product.ProductName))
                {
                    MessageBox.Show(
                        $"Barcode {row.Product.Barcode} is missing a Product Name — " +
                        "every new product needs one before saving.",
                        "Missing Name", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            ProductsToInsert = toInsert.Select(r => r.Product).ToList();
            ProductsToUpdate = toUpdate.Select(r => r.Product).ToList();
            ProductsToDelete = _pendingDeletes.Select(r => r.Product).ToList();

            SavedProducts = Rows.Select(r => r.Product).ToList();

            // FIX: NewProducts now carries each new row's Quantity (typed in
            // the grid) alongside its Product, instead of just the Product.
            // Previously the quantity typed here was discarded and the caller
            // had to default new invoice lines to Quantity = 1.
            NewProducts = Rows.Where(r => r.SourceInvoiceItem == null)
                .Select(r => (r.Product, r.Quantity))
                .ToList();

            UpdatedInvoiceLines = Rows.Where(r => r.SourceInvoiceItem != null)
                .Select(r => (r.SourceInvoiceItem!, r.Product))
                .ToList();

            // FIX: ordered snapshot of the grid, one entry per row, in the exact
            // order shown on screen. Rows (deleted rows are already gone from
            // Rows by this point) is already in the correct order — Add Copies
            // inserts each new row directly under the row it was copied from —
            // but NewProducts/UpdatedInvoiceLines above are flat lists that lose
            // that ordering. The caller uses ResultLines to rebuild PurchaseItems
            // in this same order instead of updating existing lines in place and
            // appending new ones at the end, which is why variance copies used
            // to always land at the bottom of the invoice regardless of where
            // they were created in this grid.
            ResultLines = Rows.Select(r => new BulkEditResultLine
            {
                Product = r.Product,
                SourceInvoiceItem = r.SourceInvoiceItem,
                Quantity = r.Quantity
            }).ToList();

            DialogResult = true;
            Close();
        }
        // Ordered snapshot of the grid at Save time — one entry per row, in the
        // exact order shown in the grid, so the caller can rebuild its own list
        // in the same order instead of "update in place + append new at the end".
        public class BulkEditResultLine
        {
            public MProducts Product { get; set; } = null!;
            public MPurchaseDetail? SourceInvoiceItem { get; set; } // null => brand-new line
            public double Quantity { get; set; }
        }
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}