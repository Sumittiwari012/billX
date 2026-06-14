using MyWPFCRUDApp.Models;
using MyWPFCRUDApp.ViewModels;
using System.Collections.ObjectModel;
using System.Windows;

namespace MyWPFCRUDApp.Views
{
    public partial class SupplierHistoryWindow : Window
    {
        private readonly PurchaseViewModel _vm;

        public SupplierHistoryWindow(ObservableCollection<MPurchaseMaster> history,
                                     PurchaseViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = vm;                      // so commands bind correctly
            HistoryList.ItemsSource = history;
            EmptyText.Visibility = (history == null || history.Count == 0)
                ? Visibility.Visible : Visibility.Collapsed;
        }
        private void OpenInvoice_Click(object sender, RoutedEventArgs e)
        {
            // Command already executed and loaded items into VM.
            // Just close the history window so the user sees the purchase form.
            this.Close();
        }
    }
}