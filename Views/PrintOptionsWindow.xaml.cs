using System.Windows;

namespace MyWPFCRUDApp.Views
{
    public partial class PrintOptionsWindow : Window
    {
        // Set by the caller after ShowDialog() returns true, so the caller
        // knows which option was picked and can act on it.
        public string SelectedOption { get; private set; }

        public PrintOptionsWindow()
        {
            InitializeComponent();
        }

        private void BarcodeButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedOption = "Barcode";
            DialogResult = true;
        }

        private void PurchaseBillButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedOption = "PurchaseBill";
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}