using MyWPFCRUDApp.ViewModels;
using System.Windows.Controls;

namespace MyWPFCRUDApp.Views
{
    public partial class StoreViews : UserControl
    {
        public StoreViews()
        {
            InitializeComponent();
            DataContext = new StoreViewModel();
        }
    }
}
