using MyWPFCRUDApp.Helpers;
using MyWPFCRUDApp.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace MyWPFCRUDApp.Views
{
    public partial class BarcodeLabelsWindow : Window
    {
        // Takes the invoice's line items and builds one BarcodeLabelRow per item.
        public BarcodeLabelsWindow(IEnumerable<MPurchaseDetail> items)
        {
            InitializeComponent();

            var rows = new ObservableCollection<BarcodeLabelRow>(
                items
                    .Where(i => !string.IsNullOrWhiteSpace(i.Barcode))
                    .Select(i => new BarcodeLabelRow
                    {
                        Barcode = i.Barcode,
                        Quantity = i.Quantity,
                        ProductName = i.ProductName,
                        MRP = i.MRP,
                        Retail = i.Retail,
                        BarcodeImage = BarcodeImageHelper.GenerateCode128(i.Barcode)
                    }));

            LabelsGrid.ItemsSource = rows;
        }
        private void BarcodeText_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is System.Windows.FrameworkElement fe && fe.DataContext is BarcodeLabelRow row)
            {
                row.IsLabelVisible = !row.IsLabelVisible;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}