using MyWPFCRUDApp.Helpers;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using static MyWPFCRUDApp.Services.ProductService;

namespace MyWPFCRUDApp.Views
{
    // Plain class instead of a ValueTuple — WPF's DisplayMemberPath binds via
    // reflection on the boxed runtime object, and ValueTuple element names
    // ("Key"/"Label") are compile-time-only metadata that don't survive
    // boxing to object, so DisplayMemberPath="Label" would silently show
    // nothing. A real class also lets pattern matching work normally, since
    // FieldCombo.SelectedItem is statically typed as object and object has
    // no Deconstruct the compiler can see.
    public class FilterFieldOption
    {
        public string Key { get; set; }
        public string Label { get; set; }
    }

    public partial class FilterProductsWindow : Window
    {
        // Field key -> friendly label shown in the dropdown. Key matches the
        // property read in GetFieldValue/ProductViewModel.ApplyProductFilter.
        private static readonly FilterFieldOption[] Fields =
        {
            new() { Key = "ProductName",     Label = "Product Name" },
            new() { Key = "Barcode",         Label = "Barcode" },
            new() { Key = "CategoryName",    Label = "Category" },
            new() { Key = "SubCategoryName", Label = "SubCategory" },
            new() { Key = "UnitName",        Label = "Unit" },
            new() { Key = "Colour",          Label = "Colour" },
            new() { Key = "Size",            Label = "Size" },
            new() { Key = "HSNCode",         Label = "HSN Code" },
            new() { Key = "Rack",            Label = "Rack" },
        };

        private readonly List<ProductDisplayModel> _products;

        public string SelectedFieldKey { get; private set; }
        public string SelectedValue { get; private set; }

        public FilterProductsWindow(IReadOnlyList<ProductDisplayModel> products)
        {
            InitializeComponent();
            _products = products.ToList();

            FieldCombo.ItemsSource = Fields;
            FieldCombo.DisplayMemberPath = "Label";
            FieldCombo.SelectedIndex = 0;   // triggers FieldCombo_SelectionChanged -> populates ValueCombo
        }

        private void FieldCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (FieldCombo.SelectedItem is not FilterFieldOption selected) return;

            var values = _products
                .Select(p => GetFieldValue(p, selected.Key))
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct()
                .OrderBy(v => v)
                .ToList();

            ValueCombo.ItemsSource = values;
            ValueCombo.SelectedIndex = values.Any() ? 0 : -1;

            NoValuesText.Visibility = values.Any() ? Visibility.Collapsed : Visibility.Visible;
        }

        // Single place that knows how to pull each filterable field off a
        // ProductDisplayModel — keep this in sync with the Fields array above
        // and with ProductViewModel.ApplyProductFilter's matching switch.
        private static string GetFieldValue(ProductDisplayModel p, string fieldKey) => fieldKey switch
        {
            "ProductName" => p.ProductName,
            "Barcode" => p.Barcode,
            "CategoryName" => p.CategoryName,
            "SubCategoryName" => p.SubCategoryName,
            "UnitName" => p.UnitName,
            "Colour" => p.Colour,
            "Size" => p.Size,
            "HSNCode" => p.HSNCode,
            "Rack" => p.Rack,
            _ => null
        };

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (FieldCombo.SelectedItem is not FilterFieldOption selected)
            {
                MessageBox.Show("Please choose a column to filter by.");
                return;
            }
            if (ValueCombo.SelectedItem is not string value)
            {
                MessageBox.Show("Please choose a value to filter by.");
                return;
            }

            SelectedFieldKey = selected.Key;
            SelectedValue = value;
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