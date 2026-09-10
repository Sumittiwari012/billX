using MyWPFCRUDApp.Models;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace MyWPFCRUDApp.Services
{
    // Turns a LabelTemplate + a bound data row into an actual WPF visual —
    // used for the read-only preview in BarcodeLabelsWindow, and reusable
    // later for print output so the printed label always matches what was
    // previewed.
    public static class TemplateRenderer
    {
        public const double PxPerMm = 96.0 / 25.4; // WPF device-independent pixels are 1/96 inch

        public static FrameworkElement Render(LabelTemplate template, object? dataContext)
        {
            var canvas = new Canvas
            {
                Width = template.WidthMm * PxPerMm,
                Height = template.HeightMm * PxPerMm,
                Background = Brushes.White,
                ClipToBounds = true
            };

            foreach (var el in template.Elements.OrderBy(e => e.ZIndex))
            {
                var visual = BuildVisual(el, dataContext);
                Canvas.SetLeft(visual, el.X * PxPerMm);
                Canvas.SetTop(visual, el.Y * PxPerMm);
                canvas.Children.Add(visual);
            }
            return canvas;
        }

        public static FrameworkElement BuildVisual(LabelElement el, object? dataContext)
        {
            double w = el.Width * PxPerMm;
            double h = el.Height * PxPerMm;

            FrameworkElement visual;

            switch (el.Type)
            {
                case LabelElementType.Barcode:
                    var img = new Image { Width = w, Height = h, Stretch = Stretch.Fill, DataContext = dataContext };
                    img.SetBinding(Image.SourceProperty, new Binding(el.BindingPath ?? "BarcodeImage"));
                    visual = img;
                    break;

                case LabelElementType.Text:
                    var tb = new TextBlock
                    {
                        Width = w,
                        Height = h,
                        FontFamily = new FontFamily(el.FontFamily),
                        FontSize = el.FontSize,
                        FontWeight = el.Bold ? FontWeights.Bold : FontWeights.Normal,
                        FontStyle = el.Italic ? FontStyles.Italic : FontStyles.Normal,
                        Foreground = ToBrush(el.TextColor),
                        TextAlignment = el.TextAlign switch
                        {
                            "Center" => TextAlignment.Center,
                            "Right" => TextAlignment.Right,
                            _ => TextAlignment.Left
                        },
                        TextWrapping = TextWrapping.Wrap
                    };
                    if (!string.IsNullOrEmpty(el.BindingPath))
                    {
                        tb.DataContext = dataContext;
                        var binding = new Binding(el.BindingPath);
                        if (!string.IsNullOrEmpty(el.StringFormat))
                            binding.StringFormat = "{0:" + el.StringFormat + "}";
                        tb.SetBinding(TextBlock.TextProperty, binding);
                    }
                    else
                    {
                        tb.Text = el.StaticText ?? string.Empty;
                    }
                    visual = tb;
                    break;

                case LabelElementType.Rectangle:
                    visual = new System.Windows.Shapes.Rectangle
                    {
                        Width = w,
                        Height = h,
                        Fill = ToBrush(el.FillColor),
                        Stroke = ToBrush(el.StrokeColor),
                        StrokeThickness = el.StrokeThickness
                    };
                    break;

                case LabelElementType.Ellipse:
                    visual = new System.Windows.Shapes.Ellipse
                    {
                        Width = w,
                        Height = h,
                        Fill = ToBrush(el.FillColor),
                        Stroke = ToBrush(el.StrokeColor),
                        StrokeThickness = el.StrokeThickness
                    };
                    break;

                case LabelElementType.Line:
                    visual = new System.Windows.Shapes.Line
                    {
                        // Width/Height must be set explicitly: Shape's own
                        // Width/Height default to NaN ("Auto") when unset, and
                        // the rotation center below would then evaluate to
                        // NaN, silently making the whole element invisible.
                        Width = w,
                        Height = h,
                        X1 = 0,
                        Y1 = h / 2,
                        X2 = w,
                        Y2 = h / 2,
                        Stroke = ToBrush(el.StrokeColor),
                        StrokeThickness = el.StrokeThickness
                    };
                    break;

                case LabelElementType.Image:
                    var pic = new Image { Width = w, Height = h, Stretch = Stretch.Uniform };
                    if (!string.IsNullOrEmpty(el.ImageBase64))
                        pic.Source = BitmapFromBase64(el.ImageBase64);
                    visual = pic;
                    break;

                default:
                    visual = new Border { Width = w, Height = h };
                    break;
            }

            if (el.Rotation != 0)
            {
                // Use the known w/h locals, not visual.Width/Height: some
                // shapes (e.g. Line) don't have those FrameworkElement
                // properties set explicitly, so they'd read back as NaN and
                // produce an invalid transform that hides the element.
                visual.RenderTransform = new RotateTransform(el.Rotation, w / 2, h / 2);
            }

            return visual;
        }

        public static Brush ToBrush(string? hex)
        {
            try { return hex is null ? Brushes.Transparent : (Brush)new BrushConverter().ConvertFromString(hex)!; }
            catch { return Brushes.Black; }
        }
        public static BitmapImage? BitmapFromBase64(string base64)
        {
            try
            {
                var bytes = System.Convert.FromBase64String(base64);
                using var ms = new System.IO.MemoryStream(bytes);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad; // load fully, then release the stream
                bmp.StreamSource = ms;
                bmp.EndInit();
                bmp.Freeze(); // makes it usable across threads / avoids leaks
                return bmp;
            }
            catch
            {
                return null;
            }
        }
    }
}