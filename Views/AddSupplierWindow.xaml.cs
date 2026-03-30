using System.Windows;
using WPFCRUDApp.Models;
using MyWPFCRUDApp.Services;

namespace MyWPFCRUDApp.Views
{
    public partial class AddSupplierWindow : Window
    {
        // Property to hold the supplier we just created
        public MSupplier NewSupplier { get; private set; }
        private readonly SupplierService _service = new SupplierService();

        public AddSupplierWindow()
        {
            // If this line is missing, the window will be EMPTY
            InitializeComponent();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Simple validation
            if (string.IsNullOrWhiteSpace(TxtName.Text))
            {
                MessageBox.Show("Supplier Name is required!");
                return;
            }

            NewSupplier = new MSupplier
            {
                SupplierName = TxtName.Text,
                MobileNumber = TxtMobile.Text,
                GSTIN = TxtGST.Text,
                IsActive = true
            };

            // Call the service to save to MySQL
            bool success = _service.InsertSupplier(NewSupplier);

            if (success)
            {
                this.DialogResult = true; // This closes the window and returns 'true' to the ViewModel
                this.Close();
            }
            else
            {
                MessageBox.Show("Failed to save supplier to the database.");
            }
        }
    }
}