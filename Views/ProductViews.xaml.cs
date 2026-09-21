using MyWPFCRUDApp.Helpers;
using MyWPFCRUDApp.ViewModels;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
                win.ShowDialog();
            }
        }

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
        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new FilterProductsWindow(_vm.AllProductsSnapshot)
            {
                Owner = Window.GetWindow(this)
            };

            if (dlg.ShowDialog() == true)
                _vm.ApplyProductFilter(dlg.SelectedFieldKey, dlg.SelectedValue);
        }

        private void ClearFilterButton_Click(object sender, RoutedEventArgs e)
        {
            _vm.ClearProductFilter();
        }

        // ── Purchase History overlay ────────────────────────────────────────
        // Clicking the ▾ button in the Quantity cell sets the overlay's
        // DataContext to that row's product and reveals it, centered over
        // the whole page regardless of grid scroll position.
        //
        // The overlay also lets the user re-sort the purchase batches (by
        // invoice, supplier, quantity, batch, mfg date or exp date), nudge
        // individual rows up/down with the ▲ ▼ arrows, reset back to the saved
        // order, and save the resulting order to ProductQuantity.PurchaseQuantity.
        private ProductDisplayModel? _historyProduct;
        private List<PurchaseBatchDisplay>? _originalBatchOrder;   // snapshot of the saved order
        private bool _batchOrderDirty;

        private void SetBatchOrderDirty(bool dirty)
        {
            _batchOrderDirty = dirty;
            SaveOrderButton.IsEnabled = dirty;
            ResetOrderButton.IsEnabled = dirty;   // nothing to undo unless the order has changed
        }

        // Dirty only if the current order actually differs from the saved one
        // (so moving a row and then moving it back correctly clears the flag).
        private void UpdateBatchOrderDirty()
        {
            if (_historyProduct == null || _originalBatchOrder == null) return;
            SetBatchOrderDirty(!_historyProduct.PurchaseBatches.SequenceEqual(_originalBatchOrder));
        }

        private void PurchaseHistoryButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ProductDisplayModel product)
            {
                _historyProduct = product;
                _originalBatchOrder = product.PurchaseBatches.ToList();   // snapshot for revert / dirty check
                SetBatchOrderDirty(false);

                HistoryOverlay.DataContext = product;
                HistoryOverlay.Visibility = Visibility.Visible;
            }
        }

        private void ApplySortButton_Click(object sender, RoutedEventArgs e)
        {
            if (_historyProduct == null || _originalBatchOrder == null) return;
            if (SortFieldCombo.SelectedItem is not ComboBoxItem field) return;

            bool descending = ((SortDirectionCombo.SelectedItem as ComboBoxItem)?.Tag as string) == "desc";
            _historyProduct.SortPurchaseBatches((string)field.Tag, descending);

            UpdateBatchOrderDirty();
        }

        // ▲ / ▼ arrows on each row — shift that row one position up or down.
        private void MoveBatchUp_Click(object sender, RoutedEventArgs e) => MoveBatch(sender, -1);
        private void MoveBatchDown_Click(object sender, RoutedEventArgs e) => MoveBatch(sender, +1);

        private void MoveBatch(object sender, int offset)
        {
            if (_historyProduct == null || _originalBatchOrder == null) return;
            if (sender is not Button btn || btn.Tag is not PurchaseBatchDisplay item) return;

            var list = _historyProduct.PurchaseBatches;
            int from = list.IndexOf(item);
            int to = from + offset;
            if (from < 0 || to < 0 || to >= list.Count) return;   // already at the top / bottom

            list.Move(from, to);
            UpdateBatchOrderDirty();
        }

        // Undo every sort / arrow move made since the overlay was opened
        // (or since the last Save Order) and go back to the saved order.
        private void ResetOrderButton_Click(object sender, RoutedEventArgs e)
        {
            if (_historyProduct == null || _originalBatchOrder == null) return;

            _historyProduct.ApplyBatchOrder(_originalBatchOrder);
            SetBatchOrderDirty(false);
        }

        private void SaveOrderButton_Click(object sender, RoutedEventArgs e)
        {
            if (_historyProduct == null) return;

            if (_vm.SavePurchaseBatchOrder(_historyProduct))
            {
                _originalBatchOrder = _historyProduct.PurchaseBatches.ToList();
                SetBatchOrderDirty(false);
                MessageBox.Show("Purchase order saved.", "Saved",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // Shared by the ✖ button and the backdrop click. If there's an unsaved
        // order, asks whether to save, discard or cancel before closing.
        private void CloseHistoryOverlay()
        {
            if (_batchOrderDirty && _historyProduct != null && _originalBatchOrder != null)
            {
                var r = MessageBox.Show("You have an unsaved order. Save it before closing?",
                    "Unsaved Order", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                if (r == MessageBoxResult.Cancel) return;

                if (r == MessageBoxResult.Yes)
                {
                    if (!_vm.SavePurchaseBatchOrder(_historyProduct)) return;   // save failed → stay open
                }
                else
                {
                    _historyProduct.ApplyBatchOrder(_originalBatchOrder);       // discard → back to saved order
                }
                SetBatchOrderDirty(false);
            }

            HistoryOverlay.Visibility = Visibility.Collapsed;
        }

        private void CloseHistoryOverlay_Click(object sender, RoutedEventArgs e) => CloseHistoryOverlay();

        // Clicking the dimmed backdrop (outside the card) closes the overlay.
        private void HistoryOverlay_MouseDown(object sender, MouseButtonEventArgs e) => CloseHistoryOverlay();

        // Stops a click inside the card from bubbling up to the backdrop's
        // MouseDown handler above (which would otherwise close it instantly).
        private void HistoryCard_MouseDown(object sender, MouseButtonEventArgs e) => e.Handled = true;
    }
}
