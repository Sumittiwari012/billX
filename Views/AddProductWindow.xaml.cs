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

        // ── Constructor: default (from Products page) ─────────────────────────
        public AddProductWindow()
        {
            InitializeComponent();
            LoadInitialData();
            GenerateBarcode();
        }

        // ── Constructor: from barcode scan flow ───────────────────────────────
        public AddProductWindow(string barcodeOrName)
        {
            InitializeComponent();
            LoadInitialData();

            if (!string.IsNullOrWhiteSpace(barcodeOrName))
            {
                bool looksLikeBarcode = barcodeOrName.All(c => char.IsDigit(c))
                                        && barcodeOrName.Length >= 4;
                if (looksLikeBarcode)
                {
                    _autoBarcode = barcodeOrName;
                    TxtBarcode.Text = barcodeOrName;
                }
                else
                {
                    TxtName.Text = barcodeOrName;
                    GenerateBarcode();
                }
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