using MyWPFCRUDApp.Models;
using MyWPFCRUDApp.ViewModels;
using System.Windows;
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
                if (vm.SelectedSupplier == null)
                {
                    MessageBox.Show("Please select a supplier first to view purchase history.",
                        "No Supplier", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var historyWin = new SupplierHistoryWindow(vm.SupplierHistory, vm)
                {
                    Owner = Window.GetWindow(this)
                };
                historyWin.ShowDialog();
            }
        }
    }
}
