using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZXing;
using ZXing.Common;

namespace MyWPFCRUDApp.Helpers
{
    public static class BarcodeImageHelper
    {
        public static BitmapSource GenerateCode128(string value, int width = 400, int height = 120)
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