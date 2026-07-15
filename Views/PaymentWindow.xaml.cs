using MyWPFCRUDApp.Models;
using MyWPFCRUDApp.ViewModels;
using System.Windows;
using WPFCRUDApp.Models;

namespace MyWPFCRUDApp.Views
{
    public partial class PaymentWindow : Window
    {
        public PaymentWindow(MSupplier supplier, MPurchaseMaster master)
        {
            InitializeComponent();
            DataContext = new PaymentWindowViewModel(supplier, master);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}