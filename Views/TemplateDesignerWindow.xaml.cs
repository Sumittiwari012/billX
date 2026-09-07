using MyWPFCRUDApp.Helpers;
using MyWPFCRUDApp.Models;
using MyWPFCRUDApp.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Shapes = System.Windows.Shapes;

namespace MyWPFCRUDApp.Views
{
    // One row shown in the "Product Fields" list on the left of the designer.
    public class TemplateFieldOption
    {
        public string Header { get; set; } = string.Empty;
        public string BindingPath { get; set; } = string.Empty;
        public string? StringFormat { get; set; }
        public bool IsBarcodeImage { get; set; }
        public override string ToString() => Header;
    }

    public partial class TemplateDesignerWindow : Window
    {
        private const double PxPerMm = TemplateRenderer.PxPerMm;

        // Nudge distances for arrow-key movement, in mm.
        private const double NudgeStep = 0.5;
        private const double NudgeStepFast = 5.0; // with Shift held

        // How far above the element's top edge the rotate handle floats, in px.
        private const double RotateHandleOffset = 22;
        // Rotation snaps to this many degrees when Shift is held while rotating.
        private const double RotateSnapDegrees = 15;

        private readonly LabelTemplate _template;
        private readonly BarcodeLabelRow _sampleRow;
        private readonly Dictionary<LabelElement, Border> _visuals = new();
        private LabelElement? _selected;

        private bool _isDraggingElement;
        private bool _isResizingElement;
        private bool _isRotatingElement;
        private Point _dragStartMouse;
        private double _dragStartX, _dragStartY, _dragStartW, _dragStartH, _dragStartRotation;

        // Direct refs to the position/size/rotation boxes currently shown in the
        // property panel, so drag/resize/rotate on the canvas can push live
        // values back into them.
        private TextBox? _xBox, _yBox, _wBox, _hBox, _rotBox;

        // Handed back to BarcodeLabelsWindow after a successful save.
        public LabelTemplate? SavedTemplate { get; private set; }

        public TemplateDesignerWindow(LabelTemplate? existingTemplate = null)
        {
            InitializeComponent();

            _template = existingTemplate?.Clone() ?? new LabelTemplate { Name = "New Template", WidthMm = 50, HeightMm = 25 };

            // Placeholder data so text/barcode elements show realistic-looking
            // content while designing, without needing a live invoice row.
            _sampleRow = new BarcodeLabelRow
            {
                Barcode = "8901234567890",
                ProductName = "Sample Product Name",
                Quantity = 1,
                MRP = 199.00m,
                Retail = 149.00m,
                PurchasePrice = 100.00m,
                WholesalePrice = 120.00m,
                ProductCode = "PC-001",
                HSNCode = "6109",
                Size = "M",
                Colour = "Red",
                Batch = "B-24",
                Godown = "Main",
                Rack = "R1",
                CGST = 2.5,
                SGST = 2.5,
                IGST = 0,
                BarcodeImage = BarcodeImageHelper.GenerateCode128("8901234567890")
            };

            TemplateNameBox.Text = _template.Name;
            WidthMmBox.Text = _template.WidthMm.ToString(CultureInfo.InvariantCulture);
            HeightMmBox.Text = _template.HeightMm.ToString(CultureInfo.InvariantCulture);

            FieldsList.ItemsSource = BuildFieldOptions();

            ApplyCanvasSize();
            foreach (var el in _template.Elements)
                AddVisual(el);

            // Tunnels from the Window down, so it sees every keystroke first —
            // arrow keys nudge the selected element, Delete removes it. Skipped
            // when a TextBox has focus (see Window_PreviewKeyDown).
            PreviewKeyDown += Window_PreviewKeyDown;
        }

        private static List<TemplateFieldOption> BuildFieldOptions() => new()
        {
            new() { Header = "Barcode Image",  BindingPath = "BarcodeImage",   IsBarcodeImage = true },
            new() { Header = "Barcode Number", BindingPath = "Barcode" },
            new() { Header = "Product Name",   BindingPath = "ProductName" },
            new() { Header = "Quantity",       BindingPath = "Quantity" },
            new() { Header = "MRP",            BindingPath = "MRP",            StringFormat = "N2" },
            new() { Header = "Retail",         BindingPath = "Retail",         StringFormat = "N2" },
            new() { Header = "Purchase Price", BindingPath = "PurchasePrice",  StringFormat = "N2" },
            new() { Header = "Wholesale",      BindingPath = "WholesalePrice", StringFormat = "N2" },
            new() { Header = "Product Code",   BindingPath = "ProductCode" },
            new() { Header = "HSN Code",       BindingPath = "HSNCode" },
            new() { Header = "Size",           BindingPath = "Size" },
            new() { Header = "Colour",         BindingPath = "Colour" },
            new() { Header = "Batch",          BindingPath = "Batch" },
            new() { Header = "Mfg Date",       BindingPath = "MfgDate", StringFormat = "dd-MM-yyyy" },
            new() { Header = "Exp Date",       BindingPath = "ExpDate", StringFormat = "dd-MM-yyyy" },
            new() { Header = "Godown",         BindingPath = "Godown" },
            new() { Header = "Rack",           BindingPath = "Rack" },
            new() { Header = "CGST %",         BindingPath = "CGST", StringFormat = "N1" },
            new() { Header = "SGST %",         BindingPath = "SGST", StringFormat = "N1" },
            new() { Header = "IGST %",         BindingPath = "IGST", StringFormat = "N1" },
        };

        // ── Canvas sizing — this is the "resizable by providing mms" bit ────
        private void CanvasSize_TextChanged(object sender, TextChangedEventArgs e) => ApplyCanvasSize();

        private void ApplyCanvasSize()
        {
            if (LabelSurfaceBorder == null) return; // not constructed yet

            double w = ParseMm(WidthMmBox?.Text, _template.WidthMm);
            double h = ParseMm(HeightMmBox?.Text, _template.HeightMm);
            _template.WidthMm = w;
            _template.HeightMm = h;

            double pxW = w * PxPerMm, pxH = h * PxPerMm;
            LabelSurfaceBorder.Width = pxW;
            LabelSurfaceBorder.Height = pxH;
            DesignCanvas.Width = pxW;
            DesignCanvas.Height = pxH;
            AdornerCanvas.Width = pxW;
            AdornerCanvas.Height = pxH;

            if (_selected != null) DrawAdorner(_selected);
        }

        private static double ParseMm(string? text, double fallback) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v > 0 ? v : fallback;

        // ── Adding elements ──────────────────────────────────────────────────
        private void AddText_Click(object sender, RoutedEventArgs e) => AddNewElement(new LabelElement
        {
            Type = LabelElementType.Text,
            DisplayName = "Static Text",
            StaticText = "Label",
            X = 2,
            Y = 2,
            Width = 20,
            Height = 6
        });

        private void AddRectangle_Click(object sender, RoutedEventArgs e) => AddNewElement(new LabelElement
        {
            Type = LabelElementType.Rectangle,
            DisplayName = "Rectangle",
            FillColor = "#FFFFFF",
            StrokeColor = "#000000",
            StrokeThickness = 1,
            X = 2,
            Y = 2,
            Width = 20,
            Height = 10
        });

        private void AddImage_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "PNG Images (*.png)|*.png",
                Title = "Select an image"
            };
            if (dlg.ShowDialog(this) != true) return;

            byte[] bytes;
            try
            {
                bytes = System.IO.File.ReadAllBytes(dlg.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Couldn't read that file: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string base64 = Convert.ToBase64String(bytes);

            // Default size: preserve the image's aspect ratio within a ~25mm box
            double widthMm = 25, heightMm = 25;
            var bmp = TemplateRenderer.BitmapFromBase64(base64);
            if (bmp != null && bmp.PixelWidth > 0 && bmp.PixelHeight > 0)
            {
                double aspect = (double)bmp.PixelWidth / bmp.PixelHeight;
                if (aspect >= 1) heightMm = widthMm / aspect;
                else widthMm = heightMm * aspect;
            }

            AddNewElement(new LabelElement
            {
                Type = LabelElementType.Image,
                DisplayName = "Image",
                ImageBase64 = base64,
                X = 2,
                Y = 2,
                Width = widthMm,
                Height = heightMm
            });
        }

        private void AddEllipse_Click(object sender, RoutedEventArgs e) => AddNewElement(new LabelElement
        {
            Type = LabelElementType.Ellipse,
            DisplayName = "Ellipse",
            FillColor = "#FFFFFF",
            StrokeColor = "#000000",
            StrokeThickness = 1,
            X = 2,
            Y = 2,
            Width = 12,
            Height = 12
        });

        private void AddLine_Click(object sender, RoutedEventArgs e) => AddNewElement(new LabelElement
        {
            Type = LabelElementType.Line,
            DisplayName = "Line",
            StrokeColor = "#000000",
            StrokeThickness = 1,
            X = 2,
            Y = 2,
            Width = 30,
            Height = 2
        });

        private void FieldsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (FieldsList.SelectedItem is not TemplateFieldOption field) return;

            AddNewElement(new LabelElement
            {
                Type = field.IsBarcodeImage ? LabelElementType.Barcode : LabelElementType.Text,
                DisplayName = field.Header,
                BindingPath = field.BindingPath,
                StringFormat = field.StringFormat,
                X = 2,
                Y = 2,
                Width = field.IsBarcodeImage ? 30 : 25,
                Height = field.IsBarcodeImage ? 12 : 6
            });
        }

        private void AddNewElement(LabelElement el)
        {
            el.ZIndex = _template.Elements.Count;
            // Stagger successive drops so new items don't stack exactly on top of each other.
            el.X += (_template.Elements.Count % 5) * 2;
            el.Y += (_template.Elements.Count % 5) * 2;

            _template.Elements.Add(el);
            AddVisual(el);
            Select(el);
        }

        // ── Visuals ──────────────────────────────────────────────────────────
        private void AddVisual(LabelElement el)
        {
            var border = new Border
            {
                Width = el.Width * PxPerMm,
                Height = el.Height * PxPerMm,
                // Transparent (not null) is intentional: WPF hit-tests a
                // Transparent background across the whole element, so the
                // entire box is draggable even where the rendered content
                // (e.g. an unfilled Rectangle/Ellipse outline, or a thin
                // Line) leaves gaps that wouldn't otherwise catch a click.
                Background = Brushes.Transparent,
                Cursor = Cursors.SizeAll,
                Child = TemplateRenderer.BuildVisual(el, _sampleRow),
                Tag = el
            };
            ApplyRotation(border, el);

            // Preview (tunneling) instead of the bubbling MouseLeftButtonDown:
            // guarantees this fires the instant the mouse goes down anywhere
            // inside the border's bounds, before any child content could end
            // up as the "real" hit target and change bubble-routing behavior.
            border.PreviewMouseLeftButtonDown += ElementVisual_MouseLeftButtonDown;

            Canvas.SetLeft(border, el.X * PxPerMm);
            Canvas.SetTop(border, el.Y * PxPerMm);
            Panel.SetZIndex(border, el.ZIndex);

            DesignCanvas.Children.Add(border);
            _visuals[el] = border;
        }

        private void RefreshVisual(LabelElement el)
        {
            if (!_visuals.TryGetValue(el, out var border)) return;
            border.Child = TemplateRenderer.BuildVisual(el, _sampleRow);
            border.Width = el.Width * PxPerMm;
            border.Height = el.Height * PxPerMm;
            Canvas.SetLeft(border, el.X * PxPerMm);
            Canvas.SetTop(border, el.Y * PxPerMm);
            ApplyRotation(border, el);
        }

        // Rotates the element visual around its own center, in place. Doesn't
        // change X/Y/Width/Height — those still describe the un-rotated
        // bounding box, exactly as before rotation support was added.
        private static void ApplyRotation(Border border, LabelElement el)
        {
            border.RenderTransform = el.Rotation == 0
                ? Transform.Identity
                : new RotateTransform(el.Rotation, border.Width / 2, border.Height / 2);
        }

        // ── Selection + drag-to-move ─────────────────────────────────────────
        private void ElementVisual_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border border || border.Tag is not LabelElement el) return;

            Select(el);

            _isDraggingElement = true;
            _dragStartMouse = e.GetPosition(DesignCanvas);
            _dragStartX = el.X;
            _dragStartY = el.Y;

            border.CaptureMouse();
            border.PreviewMouseMove += ElementVisual_MouseMove;
            border.PreviewMouseLeftButtonUp += ElementVisual_MouseLeftButtonUp;
            e.Handled = true;
        }

        private void ElementVisual_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDraggingElement || sender is not Border border || border.Tag is not LabelElement el) return;

            var pos = e.GetPosition(DesignCanvas);
            double newX = _dragStartX + (pos.X - _dragStartMouse.X) / PxPerMm;
            double newY = _dragStartY + (pos.Y - _dragStartMouse.Y) / PxPerMm;

            // Clamp so an element can't be dragged off the physical label.
            newX = Math.Max(0, Math.Min(newX, _template.WidthMm - el.Width));
            newY = Math.Max(0, Math.Min(newY, _template.HeightMm - el.Height));

            el.X = newX;
            el.Y = newY;

            Canvas.SetLeft(border, el.X * PxPerMm);
            Canvas.SetTop(border, el.Y * PxPerMm);
            DrawAdorner(el);
            UpdatePositionFields(el);
        }

        private void ElementVisual_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border border) return;
            _isDraggingElement = false;
            border.ReleaseMouseCapture();
            border.PreviewMouseMove -= ElementVisual_MouseMove;
            border.PreviewMouseLeftButtonUp -= ElementVisual_MouseLeftButtonUp;
        }

        private void Select(LabelElement el)
        {
            _selected = el;
            DrawAdorner(el);
            BuildPropertyPanel(el);
        }

        // ── Selection outline + resize/rotate handles ───────────────────────
        private enum HandlePos { TopLeft, TopRight, BottomLeft, BottomRight }

        private void DrawAdorner(LabelElement el)
        {
            AdornerCanvas.Children.Clear();
            if (!_visuals.ContainsKey(el)) return;

            double left = el.X * PxPerMm, top = el.Y * PxPerMm;
            double w = el.Width * PxPerMm, h = el.Height * PxPerMm;
            double centerX = left + w / 2, centerY = top + h / 2;

            // All adorner visuals (outline, resize handles, rotate handle/line)
            // are children of one group so a single RotateTransform spins the
            // whole selection UI in sync with the element itself. Their
            // Canvas.Left/Top below are plain, un-rotated coordinates — the
            // group transform is what makes them appear rotated on screen.
            var adornerGroup = new Canvas { IsHitTestVisible = true };

            var outline = new Shapes.Rectangle
            {
                Width = w,
                Height = h,
                Stroke = Brushes.DodgerBlue,
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 3, 2 },
                Fill = Brushes.Transparent,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(outline, left);
            Canvas.SetTop(outline, top);
            adornerGroup.Children.Add(outline);

            // Line from the top-center of the element up to the rotate handle,
            // purely visual — makes the handle's purpose obvious at a glance.
            var rotateLine = new Shapes.Line
            {
                X1 = centerX,
                Y1 = top,
                X2 = centerX,
                Y2 = top - RotateHandleOffset,
                Stroke = Brushes.DodgerBlue,
                StrokeThickness = 1,
                IsHitTestVisible = false
            };
            adornerGroup.Children.Add(rotateLine);

            AddResizeHandle(adornerGroup, left, top, HandlePos.TopLeft, Cursors.SizeNWSE);
            AddResizeHandle(adornerGroup, left + w, top, HandlePos.TopRight, Cursors.SizeNESW);
            AddResizeHandle(adornerGroup, left, top + h, HandlePos.BottomLeft, Cursors.SizeNESW);
            AddResizeHandle(adornerGroup, left + w, top + h, HandlePos.BottomRight, Cursors.SizeNWSE);

            AddRotateHandle(adornerGroup, centerX, top - RotateHandleOffset);

            if (el.Rotation != 0)
                adornerGroup.RenderTransform = new RotateTransform(el.Rotation, centerX, centerY);

            AdornerCanvas.Children.Add(adornerGroup);
        }

        private void AddResizeHandle(Canvas group, double centerX, double centerY, HandlePos pos, Cursor cursor)
        {
            var handle = new Shapes.Rectangle
            {
                Width = 9,
                Height = 9,
                Fill = Brushes.DodgerBlue,
                Stroke = Brushes.White,
                StrokeThickness = 1,
                Cursor = cursor,
                Tag = pos
            };
            Canvas.SetLeft(handle, centerX - 5);
            Canvas.SetTop(handle, centerY - 5);
            handle.PreviewMouseLeftButtonDown += ResizeHandle_MouseLeftButtonDown;
            group.Children.Add(handle);
        }

        private void AddRotateHandle(Canvas group, double centerX, double centerY)
        {
            var handle = new Shapes.Ellipse
            {
                Width = 12,
                Height = 12,
                Fill = Brushes.White,
                Stroke = Brushes.DodgerBlue,
                StrokeThickness = 1.5,
                Cursor = Cursors.Hand
            };
            Canvas.SetLeft(handle, centerX - 6);
            Canvas.SetTop(handle, centerY - 6);
            handle.PreviewMouseLeftButtonDown += RotateHandle_MouseLeftButtonDown;
            group.Children.Add(handle);
        }

        private HandlePos _activeHandle;

        private void ResizeHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_selected == null || sender is not Shapes.Rectangle handle) return;

            _activeHandle = (HandlePos)handle.Tag;
            _isResizingElement = true;
            _dragStartMouse = e.GetPosition(DesignCanvas);
            _dragStartX = _selected.X;
            _dragStartY = _selected.Y;
            _dragStartW = _selected.Width;
            _dragStartH = _selected.Height;

            // Capture on AdornerCanvas, not the handle itself: DrawAdorner
            // clears and rebuilds all handle shapes on every mouse move
            // (see ResizeHandle_MouseMove -> DrawAdorner), and capturing the
            // mouse on an element that then gets removed from the visual
            // tree silently drops the capture mid-drag. That was causing the
            // resize/rotate motion to keep stopping. AdornerCanvas itself is
            // never destroyed — only its children are — so capturing here
            // survives every rebuild.
            AdornerCanvas.CaptureMouse();
            AdornerCanvas.PreviewMouseMove += ResizeHandle_MouseMove;
            AdornerCanvas.PreviewMouseLeftButtonUp += ResizeHandle_MouseLeftButtonUp;
            e.Handled = true;
        }

        private void ResizeHandle_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isResizingElement || _selected == null) return;

            var pos = e.GetPosition(DesignCanvas);
            double rawDx = pos.X - _dragStartMouse.X;
            double rawDy = pos.Y - _dragStartMouse.Y;

            // The mouse delta above is in absolute canvas coordinates, but the
            // handles themselves are drawn rotated with the element. Rotate the
            // delta back by -Rotation so it lines up with the element's own
            // (un-rotated) local axes — otherwise dragging a handle on a
            // rotated shape would resize the wrong edge.
            double rad = -_selected.Rotation * Math.PI / 180.0;
            double cos = Math.Cos(rad), sin = Math.Sin(rad);
            double localDx = rawDx * cos - rawDy * sin;
            double localDy = rawDx * sin + rawDy * cos;

            double dxMm = localDx / PxPerMm;
            double dyMm = localDy / PxPerMm;

            double newX = _dragStartX, newY = _dragStartY, newW = _dragStartW, newH = _dragStartH;

            switch (_activeHandle)
            {
                case HandlePos.BottomRight:
                    newW = _dragStartW + dxMm;
                    newH = _dragStartH + dyMm;
                    break;
                case HandlePos.BottomLeft:
                    newX = _dragStartX + dxMm;
                    newW = _dragStartW - dxMm;
                    newH = _dragStartH + dyMm;
                    break;
                case HandlePos.TopRight:
                    newY = _dragStartY + dyMm;
                    newW = _dragStartW + dxMm;
                    newH = _dragStartH - dyMm;
                    break;
                case HandlePos.TopLeft:
                    newX = _dragStartX + dxMm;
                    newY = _dragStartY + dyMm;
                    newW = _dragStartW - dxMm;
                    newH = _dragStartH - dyMm;
                    break;
            }

            // Enforce a sane minimum and stop the opposite edge from flipping past it.
            const double minSize = 3;
            if (newW < minSize) { if (_activeHandle is HandlePos.TopLeft or HandlePos.BottomLeft) newX = _dragStartX + _dragStartW - minSize; newW = minSize; }
            if (newH < minSize) { if (_activeHandle is HandlePos.TopLeft or HandlePos.TopRight) newY = _dragStartY + _dragStartH - minSize; newH = minSize; }

            // Clamp to the physical label so nothing can be dragged off the page.
            newX = Math.Max(0, Math.Min(newX, _template.WidthMm - minSize));
            newY = Math.Max(0, Math.Min(newY, _template.HeightMm - minSize));
            newW = Math.Min(newW, _template.WidthMm - newX);
            newH = Math.Min(newH, _template.HeightMm - newY);

            _selected.X = newX;
            _selected.Y = newY;
            _selected.Width = newW;
            _selected.Height = newH;

            RefreshVisual(_selected);
            DrawAdorner(_selected);
            UpdatePositionFields(_selected);
            UpdateSizeFields(_selected);
        }

        private void ResizeHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isResizingElement = false;
            AdornerCanvas.ReleaseMouseCapture();
            AdornerCanvas.PreviewMouseMove -= ResizeHandle_MouseMove;
            AdornerCanvas.PreviewMouseLeftButtonUp -= ResizeHandle_MouseLeftButtonUp;
        }

        // ── Rotate-to-drag ───────────────────────────────────────────────────
        private void RotateHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_selected == null || sender is not Shapes.Ellipse handle) return;

            _isRotatingElement = true;
            _dragStartRotation = _selected.Rotation;

            // Same reasoning as the resize handle above: capture on
            // AdornerCanvas so the capture survives DrawAdorner rebuilding
            // the rotate handle shape on every mouse move.
            AdornerCanvas.CaptureMouse();
            AdornerCanvas.PreviewMouseMove += RotateHandle_MouseMove;
            AdornerCanvas.PreviewMouseLeftButtonUp += RotateHandle_MouseLeftButtonUp;
            e.Handled = true;
        }

        private void RotateHandle_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isRotatingElement || _selected == null) return;

            var pos = e.GetPosition(DesignCanvas);

            double centerX = (_selected.X + _selected.Width / 2) * PxPerMm;
            double centerY = (_selected.Y + _selected.Height / 2) * PxPerMm;

            // Angle of the mouse relative to the element's center. A vector
            // pointing straight up (the handle's rest position) corresponds
            // to 0°, so add 90 to atan2's "pointing right = 0°" convention.
            double angle = Math.Atan2(pos.Y - centerY, pos.X - centerX) * 180.0 / Math.PI + 90.0;

            angle %= 360;
            if (angle < 0) angle += 360;

            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                angle = Math.Round(angle / RotateSnapDegrees) * RotateSnapDegrees % 360;

            _selected.Rotation = angle;

            RefreshVisual(_selected);
            DrawAdorner(_selected);
            UpdateRotationField(_selected);
        }

        private void RotateHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isRotatingElement = false;
            AdornerCanvas.ReleaseMouseCapture();
            AdornerCanvas.PreviewMouseMove -= RotateHandle_MouseMove;
            AdornerCanvas.PreviewMouseLeftButtonUp -= RotateHandle_MouseLeftButtonUp;
        }

        // Clicking empty canvas space (not an element) clears the selection.
        private void DesignCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.Handled) return;
            _selected = null;
            AdornerCanvas.Children.Clear();
            PropertyPanel.Children.Clear();
            PropertyPanel.Children.Add(new TextBlock
            {
                Text = "Select an element on the canvas to edit its style.",
                Foreground = Brushes.Gray,
                FontStyle = FontStyles.Italic,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap
            });
        }

        private void DeleteSelected_Click(object sender, RoutedEventArgs e) => DeleteSelected();

        private void DeleteSelected()
        {
            if (_selected == null) return;
            if (_visuals.TryGetValue(_selected, out var border))
                DesignCanvas.Children.Remove(border);
            _visuals.Remove(_selected);
            _template.Elements.Remove(_selected);
            AdornerCanvas.Children.Clear();
            _selected = null;

            PropertyPanel.Children.Clear();
            PropertyPanel.Children.Add(new TextBlock
            {
                Text = "Select an element on the canvas to edit its style.",
                Foreground = Brushes.Gray,
                FontStyle = FontStyles.Italic,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap
            });
        }

        // ── Keyboard: arrow-key nudge + Delete ───────────────────────────────
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Don't hijack arrow keys / Delete while the user is typing in a
            // property TextBox (moving the text caret should win there).
            if (Keyboard.FocusedElement is TextBox) return;

            if (e.Key == Key.Delete)
            {
                DeleteSelected();
                e.Handled = true;
                return;
            }

            if (_selected == null) return;

            double step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? NudgeStepFast : NudgeStep;
            double dx = 0, dy = 0;

            switch (e.Key)
            {
                case Key.Left: dx = -step; break;
                case Key.Right: dx = step; break;
                case Key.Up: dy = -step; break;
                case Key.Down: dy = step; break;
                default: return; // not an arrow key — let it bubble normally
            }

            var el = _selected;
            double newX = Math.Max(0, Math.Min(el.X + dx, _template.WidthMm - el.Width));
            double newY = Math.Max(0, Math.Min(el.Y + dy, _template.HeightMm - el.Height));

            el.X = newX;
            el.Y = newY;

            if (_visuals.TryGetValue(el, out var border))
            {
                Canvas.SetLeft(border, el.X * PxPerMm);
                Canvas.SetTop(border, el.Y * PxPerMm);
            }

            DrawAdorner(el);
            UpdatePositionFields(el);
            e.Handled = true;
        }

        private void UpdatePositionFields(LabelElement el)
        {
            if (_xBox != null) _xBox.Text = el.X.ToString("0.#", CultureInfo.InvariantCulture);
            if (_yBox != null) _yBox.Text = el.Y.ToString("0.#", CultureInfo.InvariantCulture);
        }

        private void UpdateSizeFields(LabelElement el)
        {
            if (_wBox != null) _wBox.Text = el.Width.ToString("0.#", CultureInfo.InvariantCulture);
            if (_hBox != null) _hBox.Text = el.Height.ToString("0.#", CultureInfo.InvariantCulture);
        }

        private void UpdateRotationField(LabelElement el)
        {
            if (_rotBox != null) _rotBox.Text = el.Rotation.ToString("0.#", CultureInfo.InvariantCulture);
        }

        // ── Property panel ───────────────────────────────────────────────────
        private void BuildPropertyPanel(LabelElement el)
        {
            PropertyPanel.Children.Clear();

            PropertyPanel.Children.Add(new TextBlock
            {
                Text = el.DisplayName,
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 10)
            });

            AddSectionHeader("POSITION & SIZE (mm)");
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.RowDefinitions.Add(new RowDefinition());
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(6) });
            grid.RowDefinitions.Add(new RowDefinition());

            _xBox = AddLabeledNumberBox(grid, 0, 0, "X:", el.X, v => { el.X = v; RefreshVisual(el); DrawAdorner(el); });
            _yBox = AddLabeledNumberBox(grid, 0, 1, "Y:", el.Y, v => { el.Y = v; RefreshVisual(el); DrawAdorner(el); });
            _wBox = AddLabeledNumberBox(grid, 2, 0, "W:", el.Width, v => { el.Width = Math.Max(3, v); RefreshVisual(el); DrawAdorner(el); });
            _hBox = AddLabeledNumberBox(grid, 2, 1, "H:", el.Height, v => { el.Height = Math.Max(3, v); RefreshVisual(el); DrawAdorner(el); });
            PropertyPanel.Children.Add(grid);

            BuildRotationProperties(el);

            if (el.Type is LabelElementType.Text or LabelElementType.Barcode)
                BuildTextProperties(el);
            else if (el.Type == LabelElementType.Image)
                BuildImageProperties(el);
            else
                BuildShapeProperties(el);
        }

        private void BuildImageProperties(LabelElement el)
        {
            AddSectionHeader("IMAGE");

            var replaceBtn = new Button { Content = "Replace Image…", Margin = new Thickness(0, 0, 0, 10) };
            replaceBtn.Click += (_, _) =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "PNG Images (*.png)|*.png" };
                if (dlg.ShowDialog(this) != true) return;
                try
                {
                    el.ImageBase64 = Convert.ToBase64String(System.IO.File.ReadAllBytes(dlg.FileName));
                    RefreshVisual(el);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Couldn't read that file: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            PropertyPanel.Children.Add(replaceBtn);
        }

        private void BuildRotationProperties(LabelElement el)
        {
            AddSectionHeader("ROTATION (°)");

            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            _rotBox = new TextBox { Width = 55, Text = el.Rotation.ToString("0.#", CultureInfo.InvariantCulture) };
            _rotBox.TextChanged += (_, _) =>
            {
                if (double.TryParse(_rotBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                {
                    v %= 360;
                    if (v < 0) v += 360;
                    el.Rotation = v;
                    RefreshVisual(el);
                    DrawAdorner(el);
                }
            };
            row.Children.Add(_rotBox);
            PropertyPanel.Children.Add(row);

            var quickRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };

            void AddQuickButton(string label, Action apply)
            {
                var btn = new Button { Content = label, Width = 50, Margin = new Thickness(0, 0, 4, 0), Padding = new Thickness(0, 2, 0, 2) };
                btn.Click += (_, _) =>
                {
                    apply();
                    RefreshVisual(el);
                    DrawAdorner(el);
                    UpdateRotationField(el);
                };
                quickRow.Children.Add(btn);
            }

            AddQuickButton("-90°", () => el.Rotation = (el.Rotation - 90 + 360) % 360);
            AddQuickButton("+90°", () => el.Rotation = (el.Rotation + 90) % 360);
            AddQuickButton("Reset", () => el.Rotation = 0);

            PropertyPanel.Children.Add(quickRow);
        }

        private void BuildTextProperties(LabelElement el)
        {
            if (el.Type == LabelElementType.Barcode)
            {
                AddSectionHeader("SOURCE");
                PropertyPanel.Children.Add(new TextBlock
                {
                    Text = $"Bound to: {el.DisplayName}",
                    FontSize = 11,
                    Foreground = Brushes.Gray,
                    Margin = new Thickness(0, 0, 0, 10)
                });
                return; // barcode images have no font/color styling
            }

            AddSectionHeader("TEXT");

            if (!string.IsNullOrEmpty(el.BindingPath))
            {
                PropertyPanel.Children.Add(new TextBlock
                {
                    Text = $"Bound to: {el.DisplayName}",
                    FontSize = 11,
                    Foreground = Brushes.Gray,
                    Margin = new Thickness(0, 0, 0, 6)
                });
                var fmtBox = new TextBox { Text = el.StringFormat ?? "", Margin = new Thickness(0, 0, 0, 10) };
                fmtBox.TextChanged += (_, _) => { el.StringFormat = fmtBox.Text; RefreshVisual(el); };
                PropertyPanel.Children.Add(new TextBlock { Text = "Format (e.g. N2, dd-MM-yyyy):", FontSize = 10, Foreground = Brushes.Gray });
                PropertyPanel.Children.Add(fmtBox);
            }
            else
            {
                var textBox = new TextBox { Text = el.StaticText ?? "", Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, Height = 50 };
                textBox.TextChanged += (_, _) => { el.StaticText = textBox.Text; RefreshVisual(el); };
                PropertyPanel.Children.Add(textBox);
            }

            var fontCombo = new ComboBox { Margin = new Thickness(0, 0, 0, 8) };
            foreach (var f in new[] { "Segoe UI", "Arial", "Consolas", "Times New Roman", "Verdana" }) fontCombo.Items.Add(f);
            fontCombo.SelectedItem = el.FontFamily;
            if (fontCombo.SelectedIndex < 0) fontCombo.SelectedIndex = 0;
            fontCombo.SelectionChanged += (_, _) => { el.FontFamily = fontCombo.SelectedItem as string ?? "Segoe UI"; RefreshVisual(el); };
            PropertyPanel.Children.Add(fontCombo);

            var sizeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            sizeRow.Children.Add(new TextBlock { Text = "Size:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
            var sizeBox = new TextBox { Width = 50, Text = el.FontSize.ToString(CultureInfo.InvariantCulture) };
            sizeBox.TextChanged += (_, _) => { if (double.TryParse(sizeBox.Text, out var v)) { el.FontSize = v; RefreshVisual(el); } };
            sizeRow.Children.Add(sizeBox);

            var boldBtn = new ToggleButton { Content = "B", Width = 28, Margin = new Thickness(10, 0, 0, 0), IsChecked = el.Bold, FontWeight = FontWeights.Bold };
            boldBtn.Checked += (_, _) => { el.Bold = true; RefreshVisual(el); };
            boldBtn.Unchecked += (_, _) => { el.Bold = false; RefreshVisual(el); };
            sizeRow.Children.Add(boldBtn);

            var italicBtn = new ToggleButton { Content = "I", Width = 28, Margin = new Thickness(4, 0, 0, 0), IsChecked = el.Italic, FontStyle = FontStyles.Italic };
            italicBtn.Checked += (_, _) => { el.Italic = true; RefreshVisual(el); };
            italicBtn.Unchecked += (_, _) => { el.Italic = false; RefreshVisual(el); };
            sizeRow.Children.Add(italicBtn);
            PropertyPanel.Children.Add(sizeRow);

            var alignCombo = new ComboBox { Margin = new Thickness(0, 0, 0, 10) };
            foreach (var a in new[] { "Left", "Center", "Right" }) alignCombo.Items.Add(a);
            alignCombo.SelectedItem = el.TextAlign;
            if (alignCombo.SelectedIndex < 0) alignCombo.SelectedIndex = 0;
            alignCombo.SelectionChanged += (_, _) => { el.TextAlign = alignCombo.SelectedItem as string ?? "Left"; RefreshVisual(el); };
            PropertyPanel.Children.Add(alignCombo);

            AddColorPicker("Text Color", el.TextColor, hex => { el.TextColor = hex; RefreshVisual(el); });
        }

        private void BuildShapeProperties(LabelElement el)
        {
            AddSectionHeader(el.Type.ToString().ToUpperInvariant());

            if (el.Type != LabelElementType.Line)
                AddColorPicker("Fill Color", el.FillColor, hex => { el.FillColor = hex; RefreshVisual(el); });

            AddColorPicker("Stroke Color", el.StrokeColor, hex => { el.StrokeColor = hex; RefreshVisual(el); });

            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 10) };
            row.Children.Add(new TextBlock { Text = "Stroke Width:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
            var thicknessBox = new TextBox { Width = 50, Text = el.StrokeThickness.ToString(CultureInfo.InvariantCulture) };
            thicknessBox.TextChanged += (_, _) => { if (double.TryParse(thicknessBox.Text, out var v)) { el.StrokeThickness = v; RefreshVisual(el); } };
            row.Children.Add(thicknessBox);
            PropertyPanel.Children.Add(row);
        }

        private void AddSectionHeader(string text) => PropertyPanel.Children.Add(new TextBlock
        {
            Text = text,
            FontWeight = FontWeights.SemiBold,
            FontSize = 10,
            Foreground = Brushes.Gray,
            Margin = new Thickness(0, 6, 0, 6)
        });

        private TextBox AddLabeledNumberBox(Grid grid, int row, int col, string label, double value, Action<double> onChanged)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(col == 0 ? 0 : 6, 0, 0, 0) };
            panel.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Width = 16 });
            var box = new TextBox { Width = 48, Text = value.ToString("0.#", CultureInfo.InvariantCulture) };
            box.TextChanged += (_, _) => { if (double.TryParse(box.Text, out var v)) onChanged(v); };
            panel.Children.Add(box);
            Grid.SetRow(panel, row);
            Grid.SetColumn(panel, col);
            grid.Children.Add(panel);
            return box;
        }

        private void AddColorPicker(string label, string initialHex, Action<string> onChanged)
        {
            PropertyPanel.Children.Add(new TextBlock { Text = label, FontSize = 10, Foreground = Brushes.Gray, Margin = new Thickness(0, 0, 0, 4) });

            var swatchRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
            var hexBox = new TextBox { Text = initialHex, Width = 150 };

            foreach (var hex in new[] { "#000000", "#FFFFFF", "#E03131", "#2F9E44", "#1971C2", "#F08C00", "#495057", "#00000000" })
            {
                var swatch = new Button
                {
                    Width = 20,
                    Height = 20,
                    Margin = new Thickness(0, 0, 4, 4),
                    Background = TemplateRenderer.ToBrush(hex),
                    BorderBrush = Brushes.Gray,
                    BorderThickness = new Thickness(1)
                };
                swatch.Click += (_, _) => { hexBox.Text = hex; onChanged(hex); };
                swatchRow.Children.Add(swatch);
            }
            PropertyPanel.Children.Add(swatchRow);

            hexBox.LostFocus += (_, _) => onChanged(hexBox.Text);
            PropertyPanel.Children.Add(hexBox);
            PropertyPanel.Children.Add(new Border { Height = 8 });
        }

        // ── Save / Cancel ────────────────────────────────────────────────────
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var name = TemplateNameBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(this, "Please give the template a name before saving.", "Name required",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _template.Name = name;
            TemplateStorageService.Save(_template);
            SavedTemplate = _template;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}