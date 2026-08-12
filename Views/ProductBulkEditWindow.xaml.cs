using MyWPFCRUDApp.Models;
using MyWPFCRUDApp.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

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

        // The invoice line this row was built from (see BuildRows), for BOTH
        // existing DB products and not-yet-saved "skeleton" products that were
        // already on the invoice. NULL only for rows created fresh in this
        // session via "Add Copies" — those have no corresponding invoice line
        // yet, so the caller needs to add a brand-new one for them.
        //
        // This is what Save/RefreshAfterProductEdit use to report results back
        // by direct reference instead of by Barcode — matching by Barcode broke
        // whenever Add Copies renumbered a row's barcode to make room for new
        // copies, even for rows the user never touched.
        public MPurchaseDetail? SourceInvoiceItem { get; set; }

        public string StatusLabel => IsNew ? "NEW" : "EXISTING";

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

        // Columns that can never be hidden — not offered in the picker.
        private static readonly HashSet<string> AlwaysVisible = new()
        {
            "Barcode", "ProductName"
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
        public List<MProducts> NewProducts { get; private set; } = new();

        // Direct row -> invoice-line links, for every row that already had a
        // corresponding PurchaseItems entry (existing DB products AND
        // not-yet-saved products that were already on the invoice). The caller
        // updates these invoice lines by reference — not by matching Barcode,
        // since Add Copies can renumber a row's barcode even if the row itself
        // was never touched.
        public List<(MPurchaseDetail Source, MProducts Product)> UpdatedInvoiceLines { get; private set; } = new();

        public List<string> DeletedBarcodes { get; private set; } = new();

        // Rows removed via "Delete Selected" are taken out of the grid right
        // away, but the actual DB delete (for pre-existing products) is
        // deferred until Save — so Cancel still discards everything,
        // including deletes, with zero DB side effects.
        private readonly List<ProductEditRow> _pendingDeletes = new();

        public ProductBulkEditWindow(List<MPurchaseDetail> invoiceItems)
        {
            InitializeComponent();
            DataContext = this;

            LoadLookups();
            BuildColumnPicker();
            BuildRows(invoiceItems);
            BuildBulkFieldOptions();

            ProductGrid.ItemsSource = Rows;
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
                string header = col.Header?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(header) || header == "✓" || AlwaysVisible.Contains(header))
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
                    CGST = 0,
                    SGST = 0,
                    IGST = 0,
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
                        CategoryId = baseRow.Product.CategoryId,
                        SubCategoryId = baseRow.Product.SubCategoryId,
                        UnitId = baseRow.Product.UnitId,
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
                  "invoice) once you click Save All Changes — this can't be undone.\n\n" +
                  "Continue removing?"
                : $"Remove {selectedRows.Count} unsaved row(s) from this session?";

            var confirm = MessageBox.Show(warning, "Confirm Delete",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            foreach (var row in selectedRows)
            {
                if (!row.IsNew)
                    _pendingDeletes.Add(row);   // actual DB delete deferred to Save

                DeletedBarcodes.Add(row.Product.Barcode);
                Rows.Remove(row);
            }

            MessageBox.Show(
                $"Removed {selectedRows.Count} product(s) from this list.\n" +
                (existingCount > 0 ? "Existing product(s) will be deleted from the database when you Save."
                                    : ""),
                "Removed", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ── Save ─────────────────────────────────────────────────────────────
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Deletes first — if one fails, nothing else has been touched yet.
            foreach (var row in _pendingDeletes)
            {
                if (!_productService.DeleteProduct(row.Product.Id))
                {
                    MessageBox.Show(
                        $"Failed to delete '{row.Product.ProductName}' (barcode {row.Product.Barcode}). " +
                        "Stopping here — no other changes were saved either.",
                        "Delete Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

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

            foreach (var row in toInsert)
            {
                if (!_productService.InsertProduct(row.Product))
                {
                    MessageBox.Show(
                        $"Failed to save new product '{row.Product.ProductName}' " +
                        $"(barcode {row.Product.Barcode}).",
                        "Save Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                // Re-fetch to pick up the real DB-assigned Id
                var inserted = _productService.GetByBarcode(row.Product.Barcode);
                if (inserted != null) row.Product.Id = inserted.Id;
            }

            foreach (var row in toUpdate)
            {
                if (!_productService.UpdateProduct(row.Product))
                {
                    MessageBox.Show(
                        $"Failed to update product '{row.Product.ProductName}' " +
                        $"(barcode {row.Product.Barcode}).",
                        "Update Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            SavedProducts = Rows.Select(r => r.Product).ToList();
            NewProducts = Rows.Where(r => r.SourceInvoiceItem == null).Select(r => r.Product).ToList();
            UpdatedInvoiceLines = Rows.Where(r => r.SourceInvoiceItem != null)
                .Select(r => (r.SourceInvoiceItem!, r.Product))
                .ToList();

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}