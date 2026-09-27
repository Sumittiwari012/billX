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

        // NEW — the slots collection is now built ONCE and reused for the
        // life of the dialog. Switching "Vary by" used to call BuildSlots()
        // again, which created a brand-new ObservableCollection and threw
        // away every value the user had already typed (M/L/XL/XXL etc.),
        // silently resetting rows 1+ to blank. Now a field switch only
        // refreshes row 0's value (which maps back onto the currently
        // selected product) and leaves every other row's typed text alone.
        private ObservableCollection<VarianceSlotVM>? _slots;

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

        // First call (dialog just opened): creates the rows and pre-fills
        // them — slot 0 from the base row's current value for whichever
        // field is selected, slots 1..N-1 blank, quantities pre-split evenly
        // from the base row's current Quantity (remainder to the earliest
        // slots — e.g. qty 6 / 4 rows -> 2,2,1,1).
        //
        // Every SUBSEQUENT call (user switched "Vary by" after already
        // typing values) no longer rebuilds anything: it only refreshes
        // row 0's FieldValue to match the newly chosen field's current
        // value. Rows 1..N-1 — and their typed quantities — are left
        // exactly as the user left them, so switching fields can no longer
        // silently discard what was already entered.
        private void BuildSlots()
        {
            string field = ComboField.SelectedItem as string ?? "";

            if (_slots == null)
            {
                double[] distributed = DistributeQuantity(_baseQuantity, _totalCount);

                _slots = new ObservableCollection<VarianceSlotVM>();
                for (int i = 0; i < _totalCount; i++)
                {
                    _slots.Add(new VarianceSlotVM
                    {
                        Label = i == 0 ? "This item" : $"Variant {i + 1}",
                        FieldValue = i == 0 ? (_getCurrentValue(field) ?? "") : "",
                        QuantityText = distributed[i].ToString("0.##")
                    });
                }
                SlotsItemsControl.ItemsSource = _slots;
            }
            else
            {
                _slots[0].FieldValue = _getCurrentValue(field) ?? "";
            }
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
            bool anyBlank = false;

            for (int i = 0; i < _totalCount; i++)
            {
                values[i] = slots[i].FieldValue ?? "";
                if (string.IsNullOrWhiteSpace(values[i])) anyBlank = true;

                if (!double.TryParse(slots[i].QuantityText, out double qty) || qty <= 0)
                {
                    MessageBox.Show($"Row {i + 1}: enter a valid quantity greater than 0.",
                        "Invalid Quantity", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                quantities[i] = qty;
            }

            // NEW — catches exactly the scenario that caused the bug report:
            // switching "Vary by" right before saving, without noticing a
            // row's value didn't carry over the way expected. This can't
            // happen anymore for rows 1+ (see BuildSlots above), but row 0
            // can still legitimately be blank if the product's current
            // value for that field is empty — so this stays a warning you
            // can override, not a hard block.
            if (anyBlank)
            {
                var confirm = MessageBox.Show(
                    $"One or more rows have no value for '{field}'. Continue anyway?",
                    "Blank Value", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirm != MessageBoxResult.Yes) return;
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