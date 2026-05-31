using MyWPFCRUDApp.Models;
using MyWPFCRUDApp.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;

namespace MyWPFCRUDApp.Views
{
    public partial class PurchaseViews : UserControl
    {
        public PurchaseViews()
        {
            InitializeComponent();
            this.DataContext = new PurchaseViewModel();

            // Clear barcode box after each scan
            SearchBox.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        SearchBox.Clear();
                        SearchBox.Focus();
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            };
        }

        // ── Recalculate Net Amount whenever Price or Qty is edited ─────────────
        private void PurchaseDataGrid_CellEditEnding(object sender,
            DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit) return;
            if (e.Row.Item is not MPurchaseDetail item) return;

            // Give binding a chance to push the value back to the model
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // Only auto-recalculate when Price or Qty columns are edited
                string header = e.Column.Header?.ToString() ?? "";
                if (header == "Price (₹)" || header == "Qty")
                {
                    if (DataContext is PurchaseViewModel vm)
                        vm.RecalculateRow(item);
                }
                else if (header == "Net Amount (₹)")
                {
                    // User manually overrode net amount — just update grand total
                    if (DataContext is PurchaseViewModel vm2)
                    {
                        // Access via reflection-free approach: call recalc which reads AfterTaxation
                        // We only need to re-sum the total, not overwrite the user's value
                        vm2.RefreshTotal();
                    }
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
    }
}
