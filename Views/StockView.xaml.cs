using MyWPFCRUDApp.ViewModels;
using System.Windows.Controls;

namespace MyWPFCRUDApp.Views
{
    public partial class StockView : UserControl
    {
        public StockView()
        {
            InitializeComponent();
            DataContext = new StockViewModel();
        }
    }
}
