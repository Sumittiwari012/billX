using MyWPFCRUDApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace MyWPFCRUDApp.Views
{
    // Which kind of value control a field needs. Category/SubCategory/Unit
    // get a ComboBox sourced from the lists passed into the constructor;
    // everything else gets a plain TextBox.
    public enum BulkEditFieldType { Text, Category, SubCategory, Unit }

    public class BulkEditFieldDefinition
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public BulkEditFieldType Type { get; set; }
        public override string ToString() => Label; // so a plain ComboBox with no ItemTemplate still shows the label
    }

    public partial class BulkEditColumnsWindow : Window
    {
        // The full set of columns this panel can update. Category/SubCategory/
        // Unit are the "select from a list" fields the request asked for;
        // the rest are plain values (numbers or short text) typed directly.
        private static readonly List<BulkEditFieldDefinition> AvailableFields = new()
        {
            new() { Key = "CategoryId",         Label = "Category",         Type = BulkEditFieldType.Category },
            new() { Key = "SubCategoryId",      Label = "SubCategory",      Type = BulkEditFieldType.SubCategory },
            new() { Key = "UnitId",             Label = "Unit",             Type = BulkEditFieldType.Unit },
            new() { Key = "PurchasePrice",      Label = "Purchase Price",   Type = BulkEditFieldType.Text },
            new() { Key = "RetailSalePrice",    Label = "Sale Price",       Type = BulkEditFieldType.Text },
            new() { Key = "MRP",                Label = "MRP",              Type = BulkEditFieldType.Text },
            new() { Key = "CGST",               Label = "CGST %",           Type = BulkEditFieldType.Text },
            new() { Key = "SGST",               Label = "SGST %",           Type = BulkEditFieldType.Text },
            new() { Key = "IGST",               Label = "IGST %",           Type = BulkEditFieldType.Text },
            new() { Key = "DiscountPercentage", Label = "Discount %",       Type = BulkEditFieldType.Text },
            new() { Key = "Quantity",           Label = "Quantity",         Type = BulkEditFieldType.Text },
            new() { Key = "Size",               Label = "Size",             Type = BulkEditFieldType.Text },
            new() { Key = "Colour",             Label = "Colour",           Type = BulkEditFieldType.Text },
            new() { Key = "Rack",               Label = "Rack",             Type = BulkEditFieldType.Text },
            new() { Key = "HSNCode",            Label = "HSN Code",         Type = BulkEditFieldType.Text },
        };

        private readonly IEnumerable<MCategory> _categories;
        private readonly IEnumerable<MSubCategory> _subCategories;
        private readonly IEnumerable<MUnit> _units;

        // One row = a field picker + whatever value control matches the
        // currently-picked field + a remove button. Kept as a small class
        // (rather than reading back from RowsPanel.Children) so ApplyButton_Click
        // can walk a typed list instead of casting Panel children.
        private class RowState
        {
            public StackPanel RowPanel = null!;
            public ComboBox FieldCombo = null!;
            public ContentControl ValueHost = null!;
            public BulkEditFieldDefinition? Field;
        }

        private readonly List<RowState> _rows = new();

        // Populated by ApplyButton_Click; read by the caller after ShowDialog() == true.
        public List<BulkFieldUpdate> FieldUpdates { get; private set; } = new();

        public BulkEditColumnsWindow(IEnumerable<MCategory> categories, IEnumerable<MSubCategory> subCategories, IEnumerable<MUnit> units)
        {
            InitializeComponent();
            _categories = categories;
            _subCategories = subCategories;
            _units = units;

            AddRow(); // start with one empty row so the panel isn't blank
        }

        private void AddRow()
        {
            var row = new RowState();

            var fieldCombo = new ComboBox { Width = 170, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            foreach (var f in AvailableFields) fieldCombo.Items.Add(f);
            fieldCombo.SelectionChanged += (_, _) =>
            {
                row.Field = fieldCombo.SelectedItem as BulkEditFieldDefinition;
                RebuildValueControl(row);
            };

            var valueHost = new ContentControl { Width = 230, VerticalAlignment = VerticalAlignment.Center };

            var removeBtn = new Button { Content = "✖", Width = 28, Margin = new Thickness(8, 0, 0, 0) };
            removeBtn.Click += (_, _) => RemoveRow(row);

            var rowPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 8)
            };
            rowPanel.Children.Add(fieldCombo);
            rowPanel.Children.Add(valueHost);
            rowPanel.Children.Add(removeBtn);

            row.RowPanel = rowPanel;
            row.FieldCombo = fieldCombo;
            row.ValueHost = valueHost;

            RowsPanel.Children.Add(rowPanel);
            _rows.Add(row);

            fieldCombo.SelectedIndex = 0; // triggers RebuildValueControl via SelectionChanged
        }

        private void RemoveRow(RowState row)
        {
            RowsPanel.Children.Remove(row.RowPanel);
            _rows.Remove(row);
        }

        // Swaps ValueHost's content to match the newly-picked field: a
        // ComboBox sourced from the matching list for Category/SubCategory/
        // Unit, or a plain TextBox for everything else.
        private void RebuildValueControl(RowState row)
        {
            if (row.Field == null)
            {
                row.ValueHost.Content = null;
                return;
            }

            switch (row.Field.Type)
            {
                case BulkEditFieldType.Category:
                    row.ValueHost.Content = new ComboBox { ItemsSource = _categories, DisplayMemberPath = "CategoryName" };
                    break;
                case BulkEditFieldType.SubCategory:
                    row.ValueHost.Content = new ComboBox { ItemsSource = _subCategories, DisplayMemberPath = "SubCategoryName" };
                    break;
                case BulkEditFieldType.Unit:
                    row.ValueHost.Content = new ComboBox { ItemsSource = _units, DisplayMemberPath = "UnitName" };
                    break;
                default:
                    row.ValueHost.Content = new TextBox();
                    break;
            }
        }

        private void AddFieldButton_Click(object sender, RoutedEventArgs e) => AddRow();

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            var updates = new List<BulkFieldUpdate>();

            foreach (var row in _rows)
            {
                if (row.Field == null) continue;

                object? value = row.ValueHost.Content switch
                {
                    ComboBox combo => combo.SelectedItem,
                    TextBox tb => tb.Text,
                    _ => null
                };

                // Skip rows the user picked a field for but never actually
                // set a value on (empty text, or no combo selection yet).
                if (value == null) continue;
                if (value is string s && string.IsNullOrWhiteSpace(s)) continue;

                updates.Add(new BulkFieldUpdate { FieldKey = row.Field.Key, Value = value });
            }

            if (updates.Count == 0)
            {
                MessageBox.Show(this, "Pick at least one field and set its value.", "Nothing to update",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Same field picked in two rows would silently apply whichever
            // one the update loop processes last — catch it explicitly
            // instead of letting it happen quietly.
            var dupes = updates.GroupBy(u => u.FieldKey).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (dupes.Any())
            {
                MessageBox.Show(this, $"\"{string.Join(", ", dupes)}\" is selected more than once. Remove the duplicate row(s).",
                    "Duplicate field", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            FieldUpdates = updates;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
