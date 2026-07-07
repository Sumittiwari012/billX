using MyWPFCRUDApp.Models;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using static MyWPFCRUDApp.Services.ProductService;
using ZXing;
using ZXing.Common;
using System.Windows.Media.Imaging;

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
            pdtdetail.Text        = _product.ProductCode+_product.MRP.ToString("N2")+_product.Barcode;
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
        // ── Simple Code-39 style barcode renderer ─────────────────────────────
        // Draws vertical bars representing the barcode digits, stretched to
        // fill the full width of BarcodeCanvas regardless of barcode length.
        // ── Simple barcode-style renderer ───────────────────────────────────────
        // Expands each character into 8 bit-elements (its ASCII code) so the
        // pattern has enough stripes to actually resemble a barcode instead of
        // a handful of wide fence posts. 1-bits render as white bars; 0-bits are
        // left blank so the black canvas background shows through as the gap —
        // consecutive 1-bits merge into one wider bar for visual variety.
        private void DrawBarcode(string barcode)
        {
            BarcodeImage.Source = null;
            if (string.IsNullOrWhiteSpace(barcode)) return;

            var writer = new BarcodeWriterPixelData
            {
                Format = BarcodeFormat.CODE_128,
                Options = new EncodingOptions
                {
                    Height = 120,
                    Width = 400,
                    Margin = 10,
                    PureBarcode = true   // don't draw the text under the bars — TbBarcode already shows it
                }
            };

            var pixelData = writer.Write(barcode);

            var bitmap = BitmapSource.Create(
                pixelData.Width,
                pixelData.Height,
                96, 96,
                PixelFormats.Bgra32,
                null,
                pixelData.Pixels,
                pixelData.Width * 4);

            bitmap.Freeze();
            BarcodeImage.Source = bitmap;
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
