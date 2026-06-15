using MyWPFCRUDApp.ViewModels;
using System.Windows.Controls;

namespace MyWPFCRUDApp.Views
{
    public partial class PaymentMethodViews : UserControl
    {
        public PaymentMethodViews()
        {
            InitializeComponent();
            DataContext = new PaymentMethodViewModel();
        }
    }
}