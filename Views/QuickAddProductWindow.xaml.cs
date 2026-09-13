using System.Windows;
using System.Windows.Input;

namespace MyWPFCRUDApp.Views
{
    public partial class QuickAddProductWindow : Window
    {
        public string ProductName { get; private set; } = string.Empty;

        public QuickAddProductWindow()
        {
            InitializeComponent();
            Loaded += (_, _) => TxtProductName.Focus();
        }

        private void AddButton_Click(object sender, RoutedEventArgs e) => TryAccept();

        private void TxtProductName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) TryAccept();
        }

        private void TryAccept()
        {
            string name = TxtProductName.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(this, "Enter a product name.", "Product name required",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ProductName = name;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
