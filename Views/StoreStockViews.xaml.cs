using MyWPFCRUDApp.ViewModels;
using System.Windows.Controls;

namespace MyWPFCRUDApp.Views
{
    /// <summary>Read-only Store Stock screen.</summary>
    public partial class StoreStockViews : UserControl
    {
        public StoreStockViews()
        {
            InitializeComponent();
            this.DataContext = new StoreStockViewModel();
        }
    }
}
