using MyWPFCRUDApp.Helpers;
using MyWPFCRUDApp.Services;
using System.Linq;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using static MyWPFCRUDApp.Services.ProductService;
namespace MyWPFCRUDApp.Views
{
    public partial class ProductLabelPrintWindow : Window
    {
        private readonly ProductDisplayModel _product;
        private readonly PrintQueue _presetPrintQueue;

        // presetPrintQueue is optional — pass it in when printing a batch so
        // every window in the loop reuses the same queue; leave it null for
        // a single-row print, and it'll fall back to the saved default.
        public ProductLabelPrintWindow(ProductDisplayModel product, PrintQueue presetPrintQueue = null)
        {
            InitializeComponent();
            _product = product;
            _presetPrintQueue = presetPrintQueue;
            PopulateLabel();
        }
        // ── Populate label fields ──────────────────────────────────────────────
        private void PopulateLabel()
        {

            // Desc / Net Qty / Size row
            TbCategoryShort.Text = _product.Colour;
            TbNetQty.Text = "1";   // was hardcoded "1"
            TbSizeVal.Text = string.IsNullOrWhiteSpace(_product.Size) ? "-" : _product.Size;
            // Category (big)
            TbCategoryBig.Text = string.IsNullOrWhiteSpace(_product.ProductName)
                ? _product.ProductName
                : _product.CategoryName;
            // Barcode
            TbBarcodeNumber.Text = _product.Barcode;
            BarcodeImage.Source = BarcodeImageHelper.GenerateCode128(_product.Barcode);
            // Company details — pulled from Company Info if set up, else a fallback
            try
            {
                var company = new CompanyService().GetCompanyInfo().FirstOrDefault();
                if (company != null)
                {
                    TbCompanyName.Text = company.CompanyName?.ToUpper();
                    TbCompanyAddress.Text =
                        $"{company.AddressLine1} {company.City} -{company.Pincode}".Trim();
                }
                else
                {
                    TbCompanyName.Text = "MY STORE";
                    TbCompanyAddress.Text = "";
                }
            }
            catch
            {
                TbCompanyName.Text = "MY STORE";
                TbCompanyAddress.Text = "";
            }
            // MRP
            TbMRPValue.Text = _product.MRP.ToString("N0");

            // Sale Price
            //TbSalePriceValue.Text = _product.RetailSalePrice.ToString("N0");
        }
        // ── Print ──────────────────────────────────────────────────────────────
        private void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            // Use whatever queue was passed in from the batch caller, otherwise
            // fall back to the printer saved in Printer Settings.
            var queue = _presetPrintQueue ?? PrinterSettingsService.GetDefaultPrintQueue();

            if (queue == null)
            {
                MessageBox.Show("No printer is configured. Please set one in Printer Settings.",
                    "Printer Not Set", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var pd = new PrintDialog { PrintQueue = queue };

            // Pre-fill copies with the product's quantity
            int copies = (int)_product.Quantity;
            if (copies < 1) copies = 1;

            // Matches the 4.61cm x 8.38cm label size from Photoshop
            double labelWidth = MmToPx(50);
            double labelHeight = MmToPx(100);
            pd.PrintTicket.PageMediaSize = new PageMediaSize(labelWidth, labelHeight);
            PrintArea.Measure(new Size(labelWidth, labelHeight));
            PrintArea.Arrange(new Rect(0, 0, labelWidth, labelHeight));
            PrintArea.UpdateLayout();
            for (int i = 0; i < copies; i++)
            {
                pd.PrintVisual(PrintArea, $"Product Label – {_product.ProductName} [{_product.Barcode}]");
            }
            MessageBox.Show("Label sent to printer.", "Print", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        private static double MmToPx(double mm) => mm * 96.0 / 25.4;
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}