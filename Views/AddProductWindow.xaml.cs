using MyWPFCRUDApp.Models;
using MyWPFCRUDApp.Services;
using System;
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

        public AddProductWindow(string barcode)
        {
            InitializeComponent();
            TxtBarcode.Text = barcode;
            LoadInitialData();
        }

        private void LoadInitialData()
        {
            ComboCategory.ItemsSource = _categoryService.GetCategory();
            ComboUnit.ItemsSource = _unitService.GetUnit();
        }

        // Filter Subcategories when Category is selected
        private void ComboCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Use AddedItems to get the object directly instead of waiting for SelectedValue
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is MCategory selectedCategory)
            {
                long categoryId = selectedCategory.Id;

                // Get the full list from service
                var allSubs = _subCategoryService.GetSubCategoryList();

                // Filter and convert to List
                var filteredList = allSubs.Where(s => s.CategoryId == categoryId).ToList();

                // Update the ItemSource
                ComboSubCategory.ItemsSource = filteredList;

                // Select the first one if any exist
                if (filteredList.Count > 0)
                {
                    ComboSubCategory.SelectedIndex = 0;
                }
                else
                {
                    ComboSubCategory.SelectedIndex = -1;
                }
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Validate
            if (ComboSubCategory.SelectedValue == null)
            {
                MessageBox.Show("Please select a Sub-Category");
                return;
            }

            var p = new MProducts
            {
                Barcode = TxtBarcode.Text,
                ProductName = TxtName.Text,
                CategoryId = (long)ComboCategory.SelectedValue,
                SubCategoryId = (long)ComboSubCategory.SelectedValue, // Now correctly captured
                UnitId = (long)(ComboUnit.SelectedValue ?? 1),
                PurchasePrice = decimal.TryParse(TxtPurchasePrice.Text, out decimal pr) ? pr : 0,
                MRP = decimal.TryParse(TxtMRP.Text, out decimal mrp) ? mrp : 0,
                RetailSalePrice = decimal.TryParse(TxtSalePrice.Text, out decimal sp) ? sp : 0,
                CGST = double.TryParse(TxtCGST.Text, out double c) ? c : 0,
                SGST = double.TryParse(TxtSGST.Text, out double s) ? s : 0,
                CESS = double.TryParse(TxtCESS.Text, out double ces) ? ces : 0,
                HSNCode = TxtHSN.Text,
                Godown = TxtGodown.Text,
                Rack = TxtRack.Text,
                Size = TxtSize.Text,
                Colour = TxtColour.Text
            };

            if (_productService.InsertProduct(p))
            {
                this.DialogResult = true;
                this.Close();
            }
        }
    }
}