using MyWPFCRUDApp.Models;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using static MyWPFCRUDApp.Services.ProductService;

namespace MyWPFCRUDApp.Views
{
    public partial class ProductLabelPrintWindow : Window
    {
        private readonly ProductDisplayModel _product;

        public ProductLabelPrintWindow(ProductDisplayModel product)
        {
            InitializeComponent();
            _product = product;
            PopulateLabel();
        }

        // ── Populate label fields ──────────────────────────────────────────────
        private void PopulateLabel()
        {
            TbProductName.Text = _product.ProductName;
            TbBarcode.Text     = _product.Barcode;
            TbMRP.Text         = _product.MRP.ToString("N2");
            TbSale.Text        = _product.RetailSalePrice.ToString("N2");
            TbCategory.Text    = _product.CategoryName;
            TbSize.Text        = string.IsNullOrWhiteSpace(_product.Size)   ? "" : $"Size: {_product.Size}";
            TbColour.Text      = string.IsNullOrWhiteSpace(_product.Colour) ? "" : $"Colour: {_product.Colour}";

            DrawBarcode(_product.Barcode);
        }

        // ── Simple Code-39 style barcode renderer ─────────────────────────────
        // Draws vertical bars representing the barcode digits.
        // For production, swap with a proper barcode library (e.g. ZXing.Net).
        private void DrawBarcode(string barcode)
        {
            BarcodeCanvas.Children.Clear();
            if (string.IsNullOrWhiteSpace(barcode)) return;

            double canvasWidth  = 200;
            double canvasHeight = 50;
            double narrowBar    = 1.8;
            double wideBar      = 3.6;
            double gap          = 1.2;
            double x            = 0;

            foreach (char ch in barcode)
            {
                // Alternate wide/narrow based on character value parity (visual approximation)
                bool wide = (ch % 2 == 0);
                var rect = new Rectangle
                {
                    Width  = wide ? wideBar : narrowBar,
                    Height = canvasHeight,
                    Fill   = Brushes.White
                };
                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, 0);
                BarcodeCanvas.Children.Add(rect);
                x += (wide ? wideBar : narrowBar) + gap;

                if (x > canvasWidth) break;
            }
        }

        // ── Print ──────────────────────────────────────────────────────────────
        private void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            var pd = new PrintDialog();
            if (pd.ShowDialog() != true) return;

            // Set page size to label size (e.g. 4 × 2 inches at 96 dpi = 384 × 192)
            pd.PrintTicket.PageMediaSize = new PageMediaSize(PageMediaSizeName.NorthAmericaLetter);

            // Measure & arrange PrintArea for the printer page
            double pageW = pd.PrintableAreaWidth;
            double pageH = pd.PrintableAreaHeight;

            PrintArea.Measure(new Size(pageW, pageH));
            PrintArea.Arrange(new Rect(0, 0, PrintArea.DesiredSize.Width, PrintArea.DesiredSize.Height));
            PrintArea.UpdateLayout();

            pd.PrintVisual(PrintArea, $"Product Label – {_product.ProductName} [{_product.Barcode}]");

            MessageBox.Show("Label sent to printer.", "Print", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
