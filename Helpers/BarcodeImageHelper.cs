using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZXing;
using ZXing.Common;

namespace MyWPFCRUDApp.Helpers
{
    public static class BarcodeImageHelper
    {
        // FIX: bumped from 400x120 to 600x180. Label elements (both the
        // built-in templates and custom template boxes) typically render at
        // well under 400px wide — e.g. a 30mm custom box is only ~113px on
        // screen at 96 DPI — so the barcode image was being downscaled quite
        // aggressively before this change. A higher native resolution gives
        // whatever scaling filter is applied downstream (see TemplateRenderer
        // / BarcodeLabelsWindow) more source detail to work with, so the
        // resulting bar-width ratios survive the downscale more faithfully.
        public static BitmapSource GenerateCode128(string value, int width = 600, int height = 180)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            var writer = new BarcodeWriterPixelData
            {
                Format = BarcodeFormat.CODE_128,
                Options = new EncodingOptions
                {
                    Height = height,
                    Width = width,
                    Margin = 10,
                    PureBarcode = true
                }
            };

            var pixelData = writer.Write(value);

            var bitmap = BitmapSource.Create(
                pixelData.Width, pixelData.Height, 96, 96,
                PixelFormats.Bgra32, null,
                pixelData.Pixels, pixelData.Width * 4);

            bitmap.Freeze();
            return bitmap;
        }
    }
}