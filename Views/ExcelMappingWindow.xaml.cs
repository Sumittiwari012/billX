using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MyWPFCRUDApp.Views
{
    /// <summary>
    /// Interaction logic for ExcelMappingWindow.xaml
    /// </summary>
    public partial class ExcelMappingWindow : Window
    {
        public ExcelMappingWindow(object viewModel)
        {
            InitializeComponent();
            this.DataContext = viewModel;
        }

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true; // Signals the ViewModel to proceed
            this.Close();
        }
    }
}
