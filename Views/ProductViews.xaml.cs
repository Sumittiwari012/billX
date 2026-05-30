using MyWPFCRUDApp.ViewModels;
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

            // Wire column visibility: when ProductColumns[i].IsVisible changes → toggle DataGrid column
            this.Loaded += (s, e) => BindColumnVisibility();
        }

        // ── Column visibility ──────────────────────────────────────────────────
        private void BindColumnVisibility()
        {
            // Map column keys to named DataGrid columns
            var map = new Dictionary<string, DataGridColumn>
            {
                { "Barcode",         ColBarcode },
                { "ProductCode",     ColCode },
                { "ProductName",     ColName },
                { "CategoryName",    ColCategory },
                { "SubCategoryName", ColSubCategory },
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
            };

            foreach (var col in _vm.ProductColumns)
            {
                if (map.TryGetValue(col.Key, out var dgCol))
                {
                    // Initial sync
                    dgCol.Visibility = col.IsVisible ? Visibility.Visible : Visibility.Collapsed;

                    // Subscribe to future changes
                    col.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(ProductColumnOption.IsVisible))
                            dgCol.Visibility = col.IsVisible ? Visibility.Visible : Visibility.Collapsed;
                    };
                }
            }
        }

        // ── Multi-select: sync DataGrid row selection → VM SelectedItems ──────
        private void ProductDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is ProductViewModel vm)
                vm.SyncSelectedItems(ProductDataGrid.SelectedItems.Cast<ProductDisplayModel>().ToList());
        }

        // ── Per-row Print button ───────────────────────────────────────────────
        private void PrintRowButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ProductDisplayModel product)
            {
                var win = new ProductLabelPrintWindow(product);
                win.ShowDialog();
            }
        }
    }
}
