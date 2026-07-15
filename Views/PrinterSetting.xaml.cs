using MyWPFCRUDApp.Helpers;
using System.Linq;
using System.Printing;
using System.Windows;
using System.Windows.Controls;

namespace MyWPFCRUDApp.Views
{
    public partial class PrinterSetting : UserControl
    {
        public PrinterSetting()
        {
            InitializeComponent();
            LoadPrinters();
        }

        private void LoadPrinters()
        {
            var server = new LocalPrintServer();
            var queues = server.GetPrintQueues().ToList();

            PrinterComboBox.ItemsSource = queues;

            var savedName = PrinterSettingsService.GetSavedPrinterName();

            if (!string.IsNullOrWhiteSpace(savedName))
            {
                var match = queues.FirstOrDefault(q => q.FullName == savedName);
                if (match != null)
                {
                    PrinterComboBox.SelectedItem = match;
                    CurrentDefaultText.Text = $"Currently set: {match.FullName}";
                    return;
                }

                // Saved printer no longer exists (e.g. Bluetooth device unpaired)
                CurrentDefaultText.Text =
                    $"Previously saved printer \"{savedName}\" is not available. Please choose one below.";
                return;
            }

            // Nothing saved yet — default to Windows' current default printer
            var defaultQueue = server.DefaultPrintQueue;
            if (defaultQueue != null)
            {
                PrinterComboBox.SelectedItem =
                    queues.FirstOrDefault(q => q.FullName == defaultQueue.FullName);
            }

            CurrentDefaultText.Text = "No printer saved yet — select one and click Save.";
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadPrinters();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (PrinterComboBox.SelectedItem is PrintQueue selected)
            {
                PrinterSettingsService.SaveDefaultPrinter(selected.FullName);
                CurrentDefaultText.Text = $"Currently set: {selected.FullName}";
                MessageBox.Show($"Default printer set to:\n{selected.FullName}",
                    "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Please select a printer first.",
                    "No Printer Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}