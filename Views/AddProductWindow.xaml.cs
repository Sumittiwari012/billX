using MyWPFCRUDApp.Models;
using MyWPFCRUDApp.Services;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace MyWPFCRUDApp.Views
{
    public partial class AddProductWindow : Window
    {
        private readonly ProductService _productService = new ProductService();
        private readonly CategoryService _categoryService = new CategoryService();
        private readonly SubCategoryService _subCategoryService = new SubCategoryService();
        private readonly UnitService _unitService = new UnitService();

        private string _autoBarcode = string.Empty;

        // Default IDs — first record from each table, used when user leaves combo blank
        private long _defaultCategoryId = 1;
        private long _defaultSubCategoryId = 1;
        private long _defaultUnitId = 1;

        // When true, Save_Click builds the MProducts object and hands it back
        // via NewProduct WITHOUT inserting it into the database. Set only by
        // the barcode-scan constructor — that flow must stage the product and
        // let PurchaseViewModel commit it inside SavePurchase() (SAVE INVOICE),
        // exactly like Bulk Edit and Excel Import already do. Nothing should
        // hit the DB just because this dialog's Save button was clicked.
        private readonly bool _deferDatabaseInsert;

        // Populated only when _deferDatabaseInsert is true and Save succeeded.
        // The caller reads this after ShowDialog() returns true.
        public MProducts NewProduct { get; private set; }

        // ── Constructor: default (from Products page) ─────────────────────────
        public AddProductWindow()
        {
            InitializeComponent();
            LoadInitialData();
            GenerateBarcode();
        }

        // ── Constructor: from barcode scan flow ───────────────────────────────
        // Called only from PurchaseViewModel.HandleBarcodeSearch when the
        // scanned/typed value in "SCAN BARCODE / SEARCH" doesn't match any
        // existing product. That value IS the barcode the user is trying to
        // register — it should never be reinterpreted as a product name or
        // replaced with a freshly generated M-series id, no matter what
        // characters it contains (purely numeric, alphanumeric, a vendor's
        // own barcode format, etc.). The previous "looksLikeBarcode" digit
        // check was wrongly routing anything non-numeric (or under 4 chars)
        // into the Product Name box instead, then silently overwriting it
        // with a new auto-generated barcode — losing the real scanned value
        // and breaking the barcode reference this invoice line is keyed on.
        //
        // This constructor also defers the actual DB insert (see
        // _deferDatabaseInsert) — the product is only staged in-memory and
        // returned via NewProduct. It is only written to the database when
        // the invoice itself is saved via SAVE INVOICE.
        public AddProductWindow(string barcodeOrName)
        {
            InitializeComponent();
            LoadInitialData();

            _deferDatabaseInsert = true;

            if (!string.IsNullOrWhiteSpace(barcodeOrName))
            {
                _autoBarcode = barcodeOrName.Trim();
                TxtBarcode.Text = _autoBarcode;
            }
            else
            {
                GenerateBarcode();
            }
        }

        public void PreFillPurchasePrice(decimal price) { /* kept for scan-flow compatibility */ }

        // ── Barcode generation ────────────────────────────────────────────────
        private void GenerateBarcode()
        {
            try
            {
                long nextNumber = _productService.GetProductCount() + 1;
                _autoBarcode = $"M{nextNumber}";
                TxtBarcode.Text = _autoBarcode;
            }
            catch
            {
                TxtBarcode.Text = string.Empty;
            }
        }

        private void BtnRegenerate_Click(object sender, RoutedEventArgs e)
        {
            GenerateBarcode();
            TxtBarcode.Focus();
            TxtBarcode.SelectAll();
        }

        // ── Load dropdowns + capture default IDs ──────────────────────────────
        private void LoadInitialData()
        {
            var categories = _categoryService.GetCategory();
            ComboCategory.ItemsSource = categories;
            if (categories.Any())
            {
                ComboCategory.SelectedIndex = 0;          // ← this line
                _defaultCategoryId = categories.First().Id;
            }

            var subs = _subCategoryService.GetSubCategoryList();
            if (subs.Any())
                _defaultSubCategoryId = subs.First().Id;

            var units = _unitService.GetUnit();
            ComboUnit.ItemsSource = units;
            if (units.Any())
            {
                ComboUnit.SelectedIndex = 0;              // ← this line
                _defaultUnitId = units.First().Id;
            }
        }
        private void ComboCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // No sub-category picker in this form — kept so XAML wiring compiles
        }

        // ── Save ──────────────────────────────────────────────────────────────
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // ── Only Product Name is mandatory ────────────────────────────────
            if (string.IsNullOrWhiteSpace(TxtName.Text))
            {
                MessageBox.Show("Product Name is required.",
                    "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtName.Focus();
                return;
            }

            // ── Auto-generate barcode if user left it blank ───────────────────
            string barcode = string.IsNullOrWhiteSpace(TxtBarcode.Text)
                ? $"M{_productService.GetProductCount() + 1}"
                : TxtBarcode.Text.Trim();

            // ── Duplicate barcode check ───────────────────────────────────────
            // Still checked against the real DB even in deferred mode, so the
            // user is warned up front if this barcode already exists there —
            // even though nothing gets written yet.
            if (_productService.GetByBarcode(barcode) != null)
            {
                MessageBox.Show(
                    $"Barcode '{barcode}' already exists.\n" +
                    "Please edit the barcode or click ↺ to regenerate.",
                    "Duplicate Barcode", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtBarcode.Focus();
                TxtBarcode.SelectAll();
                return;
            }

            // ── Build product — every optional field has a safe default ───────
            var product = new MProducts
            {
                ProductName = TxtName.Text.Trim(),
                Barcode = barcode,

                // Category: use selection if made, else first from DB
                CategoryId = ComboCategory.SelectedValue is long catId
                                    ? catId : _defaultCategoryId,

                // SubCategory: always default (no picker in this form)
                SubCategoryId = _defaultSubCategoryId,

                // Unit: use selection if made, else first from DB
                UnitId = ComboUnit.SelectedValue is long unitId
                                    ? unitId : _defaultUnitId,

                // All amounts default to 0 — updated later from Products grid
                PurchasePrice = 0,
                RetailSalePrice = 0,
                MRP = 0,
                CGST = 0,
                SGST = 0,
                IGST = 0,
                CESS = 0,
            };

            if (_deferDatabaseInsert)
            {
                // Staged only. The caller (PurchaseViewModel) decides when this
                // actually hits the database — normally inside SavePurchase()
                // when SAVE INVOICE is clicked. Cancelling the invoice, or
                // never clicking SAVE INVOICE, means this product is never
                // written anywhere.
                NewProduct = product;
                DialogResult = true;
                Close();
                return;
            }

            if (_productService.InsertProduct(product))
            {
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Failed to save product. Please try again.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}