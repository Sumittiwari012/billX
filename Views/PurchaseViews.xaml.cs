using MyWPFCRUDApp.Models;
using MyWPFCRUDApp.Services;
using MyWPFCRUDApp.ViewModels;
using System;
using System.Windows;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;

namespace MyWPFCRUDApp.Views
{
    public partial class PurchaseViews : UserControl
    {
        public PurchaseViews()
        {
            InitializeComponent();
            DataContext = new PurchaseViewModel();
        }

        // Fires after user edits Price, Qty, or NetAmount in the grid
        // → tells ViewModel to recalculate the grand total
        private void PurchaseGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (DataContext is PurchaseViewModel vm)
                Dispatcher.BeginInvoke(new Action(() => vm.RecalculateTotal()),
                    System.Windows.Threading.DispatcherPriority.Background);
        }

        // The Supplier ComboBox is retemplated (see XAML) so "+ New Supplier"
        // is the first row inside its dropdown instead of a separate button.
        // The Command binding still opens the Add Supplier window as before —
        // this handler's only job is to close the dropdown afterward, since
        // clicking an item inside a Popup doesn't do that automatically the
        // way selecting an actual supplier does.
        private void AddSupplierMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SupplierCombo.IsDropDownOpen = false;
        }

        // ── Quick Add ────────────────────────────────────────────────────────
        // Adds a brand-new line to the invoice with nothing but a product
        // name: the barcode is generated automatically, and price/wholesale/
        // MRP/retail/quantity all start at their default values, editable
        // directly in the grid afterward — exactly like any other row.
        private void QuickAddButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not PurchaseViewModel vm) return;

            var dlg = new QuickAddProductWindow { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() != true) return;

            string nextBarcode;
            try
            {
                nextBarcode = GetNextQuickAddBarcode(vm);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not generate the next barcode: {ex.Message}",
                    "Quick Add failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var newItem = new MPurchaseDetail
            {
                Barcode = nextBarcode,
                ProductName = dlg.ProductName,
                Quantity = 1,
                PurchasePrice = 0,
                WholesalePrice = 0,
                MRP = 0,
                Retail = 0
            };

            vm.PurchaseItems.Add(newItem);
            vm.RecalculateTotal();
        }

        // ══════════════════════════════════════════════════════════════════
        // Quick Add barcode sequencing:
        //   • Always root the sequence in the REAL product master's last
        //     barcode (source of truth), read fresh via GetLastBarcode() —
        //     never trust the current cart's last item alone, since a
        //     reopened/older invoice's last item can be stale relative to
        //     barcodes newer invoices have since claimed in the DB.
        //   • Also scan every barcode already sitting in THIS cart (Quick-Add
        //     / Scan-Bill items aren't written to the DB until SAVE INVOICE,
        //     so the DB query above can't see them) and take whichever of
        //     DB-last or cart-max is higher for the matching prefix.
        //   • This guarantees no collision with either an already-saved
        //     product or another not-yet-saved line in the same cart,
        //     regardless of which invoice is being edited or in what order.
        // ══════════════════════════════════════════════════════════════════
        private string GetNextQuickAddBarcode(PurchaseViewModel vm)
        {
            // Splits a barcode into its letter-prefix and trailing numeric
            // suffix, e.g. "GR103" → ("GR", 103). Returns null if the
            // barcode doesn't end in digits (can't be sequenced).
            static (string Prefix, long Number)? Parse(string barcode)
            {
                if (string.IsNullOrWhiteSpace(barcode)) return null;
                var m = System.Text.RegularExpressions.Regex.Match(barcode, @"^(.*?)(\d+)$");
                return m.Success ? (m.Groups[1].Value, long.Parse(m.Groups[2].Value)) : null;
            }

            // 1) Source of truth: the real last barcode in the product master.
            string dbLastBarcode;
            try
            {
                dbLastBarcode = new ProductService().GetLastBarcode();
            }
            catch
            {
                dbLastBarcode = null;
            }
            var dbParsed = Parse(dbLastBarcode);

            // 2) Everything already in THIS cart — not saved to the DB yet,
            //    so the query above can't see these. Scanned/imported items
            //    with their own barcodes, plus any earlier Quick-Add rows
            //    from this same invoice-building session, all count.
            var cartParsed = vm.PurchaseItems
                .Select(i => Parse(i.Barcode))
                .Where(p => p.HasValue)
                .Select(p => p!.Value)
                .ToList();

            if (dbParsed == null && !cartParsed.Any())
            {
                // Nothing to sequence from at all — fall back to whatever
                // the generator treats as the very first barcode.
                return BarcodeGenerator.GetNext();
            }

            // Prefer the DB's own prefix (keeps continuing your existing
            // series, e.g. "GR" or "M"). If the DB has no usable barcode at
            // all, fall back to whichever prefix shows up most in the cart.
            string targetPrefix = dbParsed?.Prefix
                ?? cartParsed.GroupBy(c => c.Prefix)
                              .OrderByDescending(g => g.Count())
                              .First().Key;

            // Highest number seen for that prefix, across BOTH the DB and
            // the current cart — the next barcode goes one past that.
            long maxNumber = 0;
            if (dbParsed.HasValue && dbParsed.Value.Prefix == targetPrefix)
                maxNumber = dbParsed.Value.Number;

            long cartMax = cartParsed
                .Where(c => c.Prefix == targetPrefix)
                .Select(c => c.Number)
                .DefaultIfEmpty(0)
                .Max();

            if (cartMax > maxNumber) maxNumber = cartMax;

            return $"{targetPrefix}{maxNumber + 1}";
        }

        // ── Remove row button ─────────────────────────────────────────────────
        // FIX: Removed PreviewMouseLeftButtonDown entirely.
        //      Setting e.Handled=true there was preventing the Click event from
        //      firing — the button appeared pressed but RemoveButton_Click never ran.
        //      Com
        //      mitEdit is now called at the start of Click instead.
        private void HistoryToggle_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is PurchaseViewModel vm)
            {
                //MessageBox.Show("PurchaseViewModel found");

                var method = typeof(PurchaseViewModel)
                    .GetMethod("ToggleHistory",
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance);

                method?.Invoke(vm, null);
            }
        }
        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            // Commit any open cell edit so the grid isn't in edit mode during removal
            PurchaseGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);

            if (sender is Button btn
                && btn.Tag is MPurchaseDetail item
                && DataContext is PurchaseViewModel vm)
            {
                vm.RemoveItem(item);
            }
        }

        private void HistoryButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is PurchaseViewModel vm)
            {
                var historyWin = new SupplierHistoryWindow(vm)
                {
                    Owner = Window.GetWindow(this)
                };
                historyWin.ShowDialog();
            }
        }
        private void PrintOptionsButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new PrintOptionsWindow { Owner = Window.GetWindow(this) };
            if (win.ShowDialog() != true) return;

            var vm = DataContext as PurchaseViewModel;

            switch (win.SelectedOption)
            {
                case "Barcode":
                    if (vm == null || !vm.PurchaseItems.Any())
                    {
                        MessageBox.Show("Add at least one item to the invoice first.");
                        return;
                    }

                    var barcodeWin = new BarcodeLabelsWindow(vm.PurchaseItems)
                    {
                        Owner = Window.GetWindow(this)
                    };
                    barcodeWin.ShowDialog();
                    break;

                case "PurchaseBill":
                    MessageBox.Show("Purchase Bill print not built yet — coming in a later step.");
                    break;
            }
        }
        private void PaymentButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is PurchaseViewModel vm)
            {
                if (vm.SelectedSupplier == null)
                {
                    MessageBox.Show("Please select a supplier first.",
                        "No Supplier", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var paymentWin = new PaymentWindow(vm.SelectedSupplier, vm.PurchaseMaster)
                {
                    Owner = Window.GetWindow(this)
                };
                paymentWin.ShowDialog();
            }
        }

        // ── Edit Products (Bulk Edit) ────────────────────────────────────────
        // FIX: added CommitEdit calls before reading PurchaseItems. Most grid
        // columns already push edits into the underlying MPurchaseDetail on
        // every keystroke (UpdateSourceTrigger=PropertyChanged), so this is a
        // safety net for the rare case a cell is still actively in edit mode
        // (e.g. mid-edit, cursor still in the TextBox) at the moment ✏ Edit
        // is clicked. The actual fix for stale values showing up in Bulk Edit
        // itself is in ProductBulkEditWindow.BuildRows, which now overlays
        // each invoice line's live values onto the corresponding product
        // instead of re-fetching that product fresh from the database.
        private void EditProductsButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not PurchaseViewModel vm) return;

            PurchaseGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            PurchaseGrid.CommitEdit(DataGridEditingUnit.Row, true);

            if (!vm.PurchaseItems.Any())
            {
                MessageBox.Show("Add at least one item to the invoice first.", "No Items",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var win = new ProductBulkEditWindow(vm.PurchaseItems.ToList())
            {
                Owner = Window.GetWindow(this)
            };

            if (win.ShowDialog() == true)
            {
                // FIX: now also passes win.ResultLines — an ordered snapshot of
                // the Bulk Edit grid at Save time — so PurchaseViewModel can
                // rebuild PurchaseItems in that same order instead of updating
                // existing lines in place and appending new (variance-copy)
                // lines at the end of the invoice.
                vm.RefreshAfterProductEdit(
                    win.SavedProducts, win.NewProducts, win.DeletedBarcodes, win.UpdatedInvoiceLines,
                    win.ProductsToInsert, win.ProductsToUpdate, win.ProductsToDelete, win.ResultLines);
            }
        }
    }
}