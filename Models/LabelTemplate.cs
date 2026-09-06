using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MyWPFCRUDApp.Models
{
    public enum LabelElementType { Text, Barcode, Rectangle, Ellipse, Line }

    // One shape/text/barcode placed on the label canvas. Position and size are
    // stored in millimeters — not pixels — so the template survives being
    // re-opened at a different screen DPI or sent to a physical label printer.
    public class LabelElement : INotifyPropertyChanged
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public LabelElementType Type { get; set; } = LabelElementType.Text;

        private double _x, _y, _width = 20, _height = 8;
        public double X { get => _x; set { _x = value; OnPropertyChanged(); } }
        public double Y { get => _y; set { _y = value; OnPropertyChanged(); } }
        public double Width { get => _width; set { _width = value; OnPropertyChanged(); } }
        public double Height { get => _height; set { _height = value; OnPropertyChanged(); } }

        // ── Text / Barcode ──
        public string? BindingPath { get; set; }           // e.g. "ProductName"; null = static text
        public string DisplayName { get; set; } = "Text";  // shown in the property panel
        public string? StaticText { get; set; } = "Label";
        public string? StringFormat { get; set; }
        public string FontFamily { get; set; } = "Segoe UI";
        public double FontSize { get; set; } = 10;
        public bool Bold { get; set; }
        public bool Italic { get; set; }
        public string TextColor { get; set; } = "#000000";
        public string TextAlign { get; set; } = "Left"; // Left / Center / Right

        // ── Shapes ──
        public string FillColor { get; set; } = "#FFFFFF";
        public string StrokeColor { get; set; } = "#000000";
        public double StrokeThickness { get; set; } = 1;

        public int ZIndex { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class LabelTemplate
    {
        public string Name { get; set; } = "New Template";
        public double WidthMm { get; set; } = 50;
        public double HeightMm { get; set; } = 25;
        public ObservableCollection<LabelElement> Elements { get; set; } = new();

        public LabelTemplate Clone()
        {
            var json = System.Text.Json.JsonSerializer.Serialize(this);
            return System.Text.Json.JsonSerializer.Deserialize<LabelTemplate>(json)!;
        }
    }
}