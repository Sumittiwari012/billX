using MyWPFCRUDApp.Models;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

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

            switch (el.Type)
            {
                case LabelElementType.Barcode:
                    var img = new Image { Width = w, Height = h, Stretch = Stretch.Fill, DataContext = dataContext };
                    img.SetBinding(Image.SourceProperty, new Binding(el.BindingPath ?? "BarcodeImage"));
                    return img;

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
                    return tb;

                case LabelElementType.Rectangle:
                    return new System.Windows.Shapes.Rectangle
                    {
                        Width = w,
                        Height = h,
                        Fill = ToBrush(el.FillColor),
                        Stroke = ToBrush(el.StrokeColor),
                        StrokeThickness = el.StrokeThickness
                    };

                case LabelElementType.Ellipse:
                    return new System.Windows.Shapes.Ellipse
                    {
                        Width = w,
                        Height = h,
                        Fill = ToBrush(el.FillColor),
                        Stroke = ToBrush(el.StrokeColor),
                        StrokeThickness = el.StrokeThickness
                    };

                case LabelElementType.Line:
                    return new System.Windows.Shapes.Line
                    {
                        X1 = 0,
                        Y1 = h / 2,
                        X2 = w,
                        Y2 = h / 2,
                        Stroke = ToBrush(el.StrokeColor),
                        StrokeThickness = el.StrokeThickness
                    };

                default:
                    return new Border { Width = w, Height = h };
            }
        }

        public static Brush ToBrush(string? hex)
        {
            try { return hex is null ? Brushes.Transparent : (Brush)new BrushConverter().ConvertFromString(hex)!; }
            catch { return Brushes.Black; }
        }
    }
}