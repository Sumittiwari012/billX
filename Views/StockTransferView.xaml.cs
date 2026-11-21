using MyWPFCRUDApp.ViewModels;
using System.Windows.Controls;

namespace MyWPFCRUDApp.Views
{
    public partial class StockTransferView : UserControl
    {
        public StockTransferView()
        {
            InitializeComponent();
            DataContext = new StockTransferViewModel();
        }
    }
}
