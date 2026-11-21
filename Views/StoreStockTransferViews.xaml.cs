using MyWPFCRUDApp.ViewModels;
using System.Windows.Controls;

namespace MyWPFCRUDApp.Views
{
    /// <summary>Read-only Store Stock Transfer screen.</summary>
    public partial class StoreStockTransferViews : UserControl
    {
        public StoreStockTransferViews()
        {
            InitializeComponent();
            this.DataContext = new StoreStockTransferViewModel();
        }
    }
}
