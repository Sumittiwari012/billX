using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MyWPFCRUDApp.Views
{
    // One editable row in the dialog: a field value + a quantity, both
    // user-editable text (quantity is validated/parsed on OK).
    public class VarianceSlotVM : INotifyPropertyChanged
    {
        public string Label { get; set; } = "";

        private string _fieldValue = "";
        public string FieldValue
        {
            get => _fieldValue;
            set { if (_fieldValue != value) { _fieldValue = value; OnPropertyChanged(); } }
        }

        private string _quantityText = "";
        public string QuantityText
        {
            get => _quantityText;
            set { if (_quantityText != value) { _quantityText = value; OnPropertyChanged(); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class ProductVarianceWindow : Window
    {
        private readonly int _totalCount;
        private readonly double _baseQuantity;
        private readonly Func<string, string> _getCurrentValue;
        private readonly List<string> _fields;

        // Results — only meaningful if ShowDialog() returned true.
        public string SelectedField { get; private set; } = "";
        public string[] Values { get; private set; } = Array.Empty<string>();
        public double[] Quantities { get; private set; } = Array.Empty<double>();

        public ProductVarianceWindow(string productLabel, int totalCount, double baseQuantity,
            List<string> fields, Func<string, string> getCurrentValue)
        {
            InitializeComponent();

            _totalCount = totalCount;
            _baseQuantity = baseQuantity;
            _fields = fields;
            _getCurrentValue = getCurrentValue;

            TxtProductLabel.Text = $"Add variance — {productLabel} — {totalCount} row(s) total";

            ComboField.ItemsSource = _fields;
            if (_fields.Any())
                ComboField.SelectedIndex = 0; // triggers ComboField_SelectionChanged -> builds slots
        }

        private void ComboField_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            BuildSlots();
        }

        // Rebuilds the per-row inputs whenever the chosen field changes.
        // Slot 0 pre-fills with the base row's CURRENT value for that field,
        // since slot 0 always maps back onto the product the user selected
        // in the grid. Every other slot starts blank. Quantity is pre-filled
        // by splitting the base row's current Quantity evenly across all
        // slots — remainder goes to the earliest slots (qty 6 / 4 rows ->
        // 2,2,1,1) — and stays fully editable either way.
        private void BuildSlots()
        {
            string field = ComboField.SelectedItem as string ?? "";
            double[] distributed = DistributeQuantity(_baseQuantity, _totalCount);

            var slots = new ObservableCollection<VarianceSlotVM>();
            for (int i = 0; i < _totalCount; i++)
            {
                slots.Add(new VarianceSlotVM
                {
                    Label = i == 0 ? "This item" : $"Variant {i + 1}",
                    FieldValue = i == 0 ? (_getCurrentValue(field) ?? "") : "",
                    QuantityText = distributed[i].ToString("0.##")
                });
            }
            SlotsItemsControl.ItemsSource = slots;
        }

        private static double[] DistributeQuantity(double totalQty, int count)
        {
            var result = new double[count];
            if (count <= 0) return result;

            long totalWhole = (long)Math.Round(totalQty);
            bool isWhole = Math.Abs(totalQty - totalWhole) < 0.0001 && totalWhole >= count;

            if (isWhole)
            {
                long each = totalWhole / count;
                long remainder = totalWhole % count;
                for (int i = 0; i < count; i++)
                    result[i] = each + (i < remainder ? 1 : 0);
            }
            else
            {
                // Fractional quantity, or fewer units than rows — just split
                // evenly; there's no clean "whole unit" remainder to front-load.
                double each = totalQty / count;
                for (int i = 0; i < count; i++)
                    result[i] = each;
            }
            return result;
        }

        // Enter moves focus to the next field instead of doing nothing (the
        // WPF default for a plain TextBox) — Value -> Quantity -> next row's
        // Value -> next row's Quantity, following the same order the fields
        // are laid out in. Pressing Enter on the very last field (the last
        // row's Quantity box) submits the dialog, same as clicking
        // "Add Variance".
        private void Input_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;

            if (sender is not UIElement el) return;

            bool moved = el.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            if (!moved)
                Ok_Click(sender, e);
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            string field = ComboField.SelectedItem as string ?? "";
            if (string.IsNullOrEmpty(field))
            {
                MessageBox.Show("Pick a field to vary by first.", "No Field Selected",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (SlotsItemsControl.ItemsSource is not ObservableCollection<VarianceSlotVM> slots
                || slots.Count != _totalCount)
            {
                return;
            }

            var values = new string[_totalCount];
            var quantities = new double[_totalCount];

            for (int i = 0; i < _totalCount; i++)
            {
                values[i] = slots[i].FieldValue ?? "";

                if (!double.TryParse(slots[i].QuantityText, out double qty) || qty <= 0)
                {
                    MessageBox.Show($"Row {i + 1}: enter a valid quantity greater than 0.",
                        "Invalid Quantity", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                quantities[i] = qty;
            }

            SelectedField = field;
            Values = values;
            Quantities = quantities;

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
