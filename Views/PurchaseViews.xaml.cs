using MyWPFCRUDApp.ViewModels;
using System.Windows.Controls;

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
        private void PurchaseGrid_CellEditEnding(object sender,
            DataGridCellEditEndingEventArgs e)
        {
            if (DataContext is PurchaseViewModel vm)
                vm.RecalculateTotal();
        }
    }
}