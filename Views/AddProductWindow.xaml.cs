using MyWPFCRUDApp.Models;
using MyWPFCRUDApp.Services;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace MyWPFCRUDApp.Views
{
    public partial class AddProductWindow : Window
    {
        private readonly ProductService     _productService     = new ProductService();
        private readonly CategoryService    _categoryService    = new CategoryService();
        private readonly SubCategoryService _subCategoryService = new SubCategoryService();
        private readonly UnitService        _unitService        = new UnitService();

        // Constructor used by barcode search (barcode is a real barcode)
        public AddProductWindow(string barcodeOrName)
        {
            InitializeComponent();

            // If the string looks like a barcode (numeric / short), put it in barcode field
            // Otherwise treat it as a product name suggestion
            if (!string.IsNullOrWhiteSpace(barcodeOrName))
            {
                bool looksLikeBarcode = barcodeOrName.All(c => char.IsDigit(c)) &&
                                        barcodeOrName.Length >= 4;
                if (looksLikeBarcode)
                    TxtBarcode.Text = barcodeOrName;
                else
                    TxtName.Text = barcodeOrName;   // pre-fill name from scanned description
            }

            LoadInitialData();
        }

        /// <summary>Pre-fill the purchase price field (called from scan flow).</summary>
        public void PreFillPurchasePrice(decimal price)
        {
            if (price > 0)
                TxtPurchasePrice.Text = price.ToString("N2");
        }

        private void LoadInitialData()
        {
            ComboCategory.ItemsSource = _categoryService.GetCategory();
            ComboUnit.ItemsSource     = _unitService.GetUnit();
        }

        // Filter Subcategories when Category is selected
        private void ComboCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is MCategory selectedCategory)
            {
                long categoryId  = selectedCategory.Id;
                var  allSubs     = _subCategoryService.GetSubCategoryList();
                var  filteredList = allSubs.Where(s => s.CategoryId == categoryId).ToList();

                ComboSubCategory.ItemsSource = filteredList;
                ComboSubCategory.SelectedIndex = filteredList.Count > 0 ? 0 : -1;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (ComboSubCategory.SelectedValue == null)
            {
                MessageBox.Show("Please select a Sub-Category");
                return;
            }

            if (string.IsNullOrWhiteSpace(TxtName.Text))
            {
                MessageBox.Show("Product Name is required.");
                return;
            }

            var p = new MProducts
            {
                Barcode          = TxtBarcode.Text,
                ProductName      = TxtName.Text,
                CategoryId       = (long)ComboCategory.SelectedValue,
                SubCategoryId    = (long)ComboSubCategory.SelectedValue,
                UnitId           = (long)(ComboUnit.SelectedValue ?? 1),
                PurchasePrice    = decimal.TryParse(TxtPurchasePrice.Text, out decimal pr)  ? pr  : 0,
                MRP              = decimal.TryParse(TxtMRP.Text,           out decimal mrp) ? mrp : 0,
                RetailSalePrice  = decimal.TryParse(TxtSalePrice.Text,     out decimal sp)  ? sp  : 0,
                CGST             = double.TryParse(TxtCGST.Text,           out double c)    ? c   : 0,
                SGST             = double.TryParse(TxtSGST.Text,           out double s)    ? s   : 0,
                CESS             = double.TryParse(TxtCESS.Text,           out double ces)  ? ces : 0,
                HSNCode          = TxtHSN.Text,
                Godown           = TxtGodown.Text,
                Rack             = TxtRack.Text,
                Size             = TxtSize.Text,
                Colour           = TxtColour.Text
            };

            if (_productService.InsertProduct(p))
            {
                this.DialogResult = true;
                this.Close();
            }
        }
    }
}
