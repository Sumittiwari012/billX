using MyWPFCRUDApp.Helpers;
using MyWPFCRUDApp.ViewModels;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using static MyWPFCRUDApp.Services.ProductService;

namespace MyWPFCRUDApp.Views
{
    public partial class ProductViews : UserControl
    {
        private ProductViewModel _vm;
        public ProductViews()
        {
            InitializeComponent();
            _vm = new ProductViewModel();
            this.DataContext = _vm;
            this.Loaded += (s, e) => BindColumnVisibility();
        }
        private void BindColumnVisibility()
        {
            var map = new Dictionary<string, DataGridColumn>
            {
                { "Barcode",         ColBarcode },
                { "ProductCode",     ColCode },
                { "ProductName",     ColName },
                { "CategoryName",    ColCategory },
                { "SubCategoryName", ColSubCategory },
                { "Quantity",        ColQuantity },
                { "PurchasePrice",   ColPurchase },
                { "RetailSalePrice", ColSale },
                { "MRP",             ColMRP },
                { "CGST",            ColCGST },
                { "SGST",            ColSGST },
                { "IGST",            ColIGST },
                { "UnitName",        ColUnit },
                { "Size",            ColSize },
                { "Colour",          ColColour },
                { "Rack",            ColRack },
                { "HSNCode",         ColHSN },
                { "DiscountPercentage",Coldis}
            };
            foreach (var col in _vm.ProductColumns)
            {
                if (map.TryGetValue(col.Key, out var dgCol))
                {
                    dgCol.Visibility = col.IsVisible ? Visibility.Visible : Visibility.Collapsed;
                    col.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(ProductColumnOption.IsVisible))
                            dgCol.Visibility = col.IsVisible ? Visibility.Visible : Visibility.Collapsed;
                    };
                }
            }
        }
        private void PrintRowButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ProductDisplayModel product)
            {
                var win = new ProductLabelPrintWindow(product);
                win.ShowDialog();
            }
        }
        private void PrintSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = _vm.CheckedProducts.ToList();
            if (!selected.Any())
            {
                MessageBox.Show("Please select at least one product to print.",
                    "No Products Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            // Resolve the saved default printer once for the whole batch
            var queue = PrinterSettingsService.GetDefaultPrintQueue();
            if (queue == null)
            {
                MessageBox.Show("No printer is configured. Please set one in Printer Settings.",
                    "Printer Not Set", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            foreach (var product in selected)
            {
                var win = new ProductLabelPrintWindow(product, queue);
                win.ShowDialog();   // blocks here — next window opens only after this one is closed
            }
        }

        // Opens the bulk-edit panel (BulkEditColumnsWindow), where the user
        // picks any number of columns and a value for each — Category,
        // SubCategory, and Unit are dropdowns sourced from the same lists
        // the entry form uses, everything else is typed directly. On
        // confirm, hands the picked (field, value) pairs to the view model
        // to apply across every checked row.
        private void UpdateColumnsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_vm.CheckedProducts.Any())
            {
                MessageBox.Show("Please select at least one product to update.",
                    "No Products Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new BulkEditColumnsWindow(_vm.Categories, _vm.AllSubCategories, _vm.Units)
            {
                Owner = Window.GetWindow(this)
            };

            if (dlg.ShowDialog() == true)
                _vm.ApplyBulkColumnUpdates(dlg.FieldUpdates);
        }
    }
}
