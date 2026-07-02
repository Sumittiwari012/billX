using ClosedXML.Excel;
using ExcelDataReader;
using Microsoft.Win32;
using MyWPFCRUDApp.Helpers;
using MyWPFCRUDApp.Models;
using MyWPFCRUDApp.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using static MyWPFCRUDApp.Services.ProductService;
using static MyWPFCRUDApp.Services.SubCategoryService;

namespace MyWPFCRUDApp.ViewModels
{
    public class ProductViewModel : BaseViewModel
    {
        // ─── Commands ──────────────────────────────────────────────────────────
        public ICommand ProductSaveCommand { get; }
        public ICommand ProductDeleteCommand { get; }
        public ICommand ProductResetCommand { get; }
        public ICommand ImportExcelCommand => new RelayCommand(_ => ExecuteImportWizard());
        public ICommand ExportExcelCommand => new RelayCommand(_ => ExportToExcel());
        public ICommand DeleteSelectedCommand => new RelayCommand(_ => DeleteSelected(), _ => CheckedProducts.Any());
        public ICommand ClearSelectionCommand => new RelayCommand(_ => ClearSelection());
        private long _quantity;
        public long Quantity
        {
            get => _quantity;
            set
            {
                if (_quantity != value)
                {
                    _quantity = value;
                    OnPropertyChanged();
                }
            }
        }
        // Master, unfiltered list — the source of truth for the live barcode filter.
        // Products is always derived from this, never edited directly.
        private List<ProductDisplayModel> _allProducts = new();

        // The barcode auto-generated for the next new product. Restored into
        // BarcodeInput whenever the user clears the textbox entirely.
        private string _lastGeneratedBarcode = string.Empty;
        private long _quantityInput;
        public long QuantityInput
        {
            get => _quantityInput;
            set => SetProperty(ref _quantityInput, value);
        }

        // ─── Services ──────────────────────────────────────────────────────────
        private readonly ProductService _productService;
        private readonly CategoryService _categoryService;
        private readonly SubCategoryService _subCategoryService;
        private readonly UnitService _unitService;

        // ─── Product List (DataGrid) ───────────────────────────────────────────
        private ObservableCollection<ProductDisplayModel> _products;
        public ObservableCollection<ProductDisplayModel> Products
        {
            get => _products;
            set => SetProperty(ref _products, value);
        }

        // ─── Multi-select tracking ─────────────────────────────────────────────
        // The code-behind calls SyncSelectedItems() on SelectionChanged.
        private List<ProductDisplayModel> _checkedProducts = new();
        public IReadOnlyList<ProductDisplayModel> CheckedProducts => _checkedProducts;

        public void SyncSelectedItems(List<ProductDisplayModel> selected)
        {
            _checkedProducts = selected;
            OnPropertyChanged(nameof(SelectedCountText));
            OnPropertyChanged(nameof(MultiSelectBarVisibility));
        }

        public string SelectedCountText =>
            _checkedProducts.Count > 0
                ? $"{_checkedProducts.Count} product(s) selected"
                : string.Empty;

        public Visibility MultiSelectBarVisibility =>
            _checkedProducts.Count > 1 ? Visibility.Visible : Visibility.Collapsed;

        // "Select All" header checkbox support
        // "Select All" header checkbox support
        private bool? _allSelected = false;
        public bool? AllSelected
        {
            get => _allSelected;
            set
            {
                if (SetProperty(ref _allSelected, value))
                {
                    bool select = value.GetValueOrDefault();
                    if (Products != null)
                    {
                        foreach (var p in Products)
                            p.IsSelected = select;
                    }
                }
            }
        }
        // ─── Barcode entry — drives the live filter as the user types ─────────
        private string _barcodeInput = string.Empty;
        public string BarcodeInput
        {
            get => _barcodeInput;
            set
            {
                if (SetProperty(ref _barcodeInput, value))
                {
                    MProduct.Barcode = value;
                    FilterProductsByBarcode(value);
                }
            }
        }

        private void FilterProductsByBarcode(string barcodeText)
        {
            if (_allProducts == null) return;

            if (string.IsNullOrWhiteSpace(barcodeText))
            {
                // Box was cleared — show every product, and bring back whatever
                // barcode was waiting before the user started typing.
                Products = new ObservableCollection<ProductDisplayModel>(_allProducts);

                if (!string.IsNullOrWhiteSpace(_lastGeneratedBarcode))
                    BarcodeInput = _lastGeneratedBarcode;   // re-enters setter once; non-empty, so no further recursion
                return;
            }

            var matches = _allProducts
                .Where(p => !string.IsNullOrEmpty(p.Barcode) &&
                            p.Barcode.IndexOf(barcodeText, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            // Found something → narrow the grid. Found nothing → this is a brand
            // new barcode, so show everything as-is and let the user fill in the
            // rest of the form to add it as a new product.
            Products = matches.Any()
                ? new ObservableCollection<ProductDisplayModel>(matches)
                : new ObservableCollection<ProductDisplayModel>(_allProducts);
        }

        // ─── Column Visibility ─────────────────────────────────────────────────
        public ObservableCollection<ProductColumnOption> ProductColumns { get; } = new();

        private void InitProductColumns()
        {
            var cols = new[]
            {
        ("Barcode",         "Barcode"),
        ("ProductCode",     "Product Code"),
        ("ProductName",     "Product Name"),
        ("CategoryName",    "Category"),
        ("SubCategoryName", "SubCategory"),
        ("Quantity",        "Quantity"),     // ← NEW
        ("PurchasePrice",   "Purchase"),
        ("RetailSalePrice", "Sale"),
        ("MRP",             "MRP"),
        ("CGST",            "CGST"),
        ("SGST",            "SGST"),
        ("IGST",            "IGST"),
        ("UnitName",        "Unit"),
        ("Size",            "Size"),
        ("Colour",          "Colour"),
        ("Rack",            "Rack"),
        ("HSNCode",         "HSN"),
    };

            foreach (var (key, header) in cols)
                ProductColumns.Add(new ProductColumnOption { Key = key, Header = header, IsVisible = true });
        }

        // ─── Excel Mapping ─────────────────────────────────────────────────────
        private ObservableCollection<string> _excelHeaders;
        public ObservableCollection<string> ExcelHeaders
        {
            get => _excelHeaders;
            set => SetProperty(ref _excelHeaders, value);
        }

        public ObservableCollection<ColumnMapping> Mappings { get; set; } = new();

        // ─── Selected Row ──────────────────────────────────────────────────────
        private ProductDisplayModel _selectedProduct;
        public ProductDisplayModel SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                if (SetProperty(ref _selectedProduct, value) && value != null)
                {
                    MProduct = new MProducts
                    {
                        Id = value.Id,
                        ProductCode = value.ProductCode,
                        ProductName = value.ProductName,
                        Barcode = value.Barcode,
                        CategoryId = value.CategoryId,
                        SubCategoryId = value.SubCategoryId,
                        UnitId = value.UnitId,
                        PurchasePrice = value.PurchasePrice,
                        RetailSalePrice = value.RetailSalePrice,
                        WholesalePrice = value.WholesalePrice,
                        MRP = value.MRP,
                        DiscountPercentage = value.DiscountPercentage,
                        CGST = value.CGST,
                        SGST = value.SGST,
                        IGST = value.IGST,
                        CESS = value.CESS,
                        HSNCode = value.HSNCode,
                        PartGroup = value.PartGroup,
                        Description = value.Description,
                        Godown = value.Godown,
                        Rack = value.Rack,
                        Batch = value.Batch,
                        MfgDate = value.MfgDate,
                        ExpDate = value.ExpDate,
                        Size = value.Size,
                        Colour = value.Colour,
                        IMEI1 = value.IMEI1,
                        IMEI2 = value.IMEI2,
                    };

                    // Reflect the barcode in the textbox WITHOUT re-triggering the
                    // live filter — otherwise clicking a row would narrow the grid
                    // down to just that single product.
                    _barcodeInput = value.Barcode ?? string.Empty;
                    OnPropertyChanged(nameof(BarcodeInput));

                    SelectedCategory = Categories.FirstOrDefault(c => c.Id == value.CategoryId);
                    SelectedSubCategory = FilteredSubCategories.FirstOrDefault(s => s.Id == value.SubCategoryId);
                    SelectedUnit = Units.FirstOrDefault(u => u.Id == value.UnitId);

                    _quantityInput = value.Quantity;                 // NEW — avoid re-triggering setter logic
                    OnPropertyChanged(nameof(QuantityInput));
                }
            }
        }

        // ─── Form Model ────────────────────────────────────────────────────────
        private MProducts _mProduct;
        public MProducts MProduct
        {
            get => _mProduct;
            set
            {
                if (SetProperty(ref _mProduct, value))
                {
                    // Reset percentages when product changes
                    WholesalePricePercentage = 0;
                    MRPPercentage = 0;
                }
            }
        }

        // ─── Price Percentage Auto-Calculation ──────────────────────────────
        private double _wholesalePricePercentage = 0;
        public double WholesalePricePercentage
        {
            get => _wholesalePricePercentage;
            set
            {
                if (SetProperty(ref _wholesalePricePercentage, value))
                    RecalculateWholesalePrice();
            }
        }

        private double _mrpPercentage = 0;
        public double MRPPercentage
        {
            get => _mrpPercentage;
            set
            {
                if (SetProperty(ref _mrpPercentage, value))
                    RecalculateMRP();
            }
        }

        // Auto-calculate wholesale price from purchase price and percentage
        private void RecalculateWholesalePrice()
        {
            if (MProduct != null && MProduct.PurchasePrice > 0 && WholesalePricePercentage > 0)
            {
                MProduct.WholesalePrice = MProduct.PurchasePrice * ((decimal)WholesalePricePercentage / 100);
                OnPropertyChanged(nameof(MProduct));
            }
        }

        // Auto-calculate MRP from purchase price and percentage
        private void RecalculateMRP()
        {
            if (MProduct != null && MProduct.PurchasePrice > 0 && MRPPercentage > 0)
            {
                MProduct.MRP = MProduct.PurchasePrice * ((decimal)MRPPercentage / 100);
                OnPropertyChanged(nameof(MProduct));
            }
        }

        // ─── Category Dropdown ─────────────────────────────────────────────────
        private ObservableCollection<MCategory> _categories;
        public ObservableCollection<MCategory> Categories
        {
            get => _categories;
            set => SetProperty(ref _categories, value);
        }

        private MCategory _selectedCategory;
        public MCategory SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value))
                {
                    if (value != null)
                    {
                        MProduct.CategoryId = value.Id;
                        FilteredSubCategories = new ObservableCollection<MSubCategory>(
                            _allSubCategories.Where(s => s.CategoryId == value.Id));
                    }
                    else
                    {
                        FilteredSubCategories = new ObservableCollection<MSubCategory>();
                    }
                    SelectedSubCategory = null;
                }
            }
        }

        // ─── SubCategory Dropdown ──────────────────────────────────────────────
        private List<MSubCategory> _allSubCategories = new();

        private ObservableCollection<MSubCategory> _filteredSubCategories;
        public ObservableCollection<MSubCategory> FilteredSubCategories
        {
            get => _filteredSubCategories;
            set => SetProperty(ref _filteredSubCategories, value);
        }

        private MSubCategory _selectedSubCategory;
        public MSubCategory SelectedSubCategory
        {
            get => _selectedSubCategory;
            set
            {
                if (SetProperty(ref _selectedSubCategory, value) && value != null)
                    MProduct.SubCategoryId = value.Id;
            }
        }

        // ─── Unit Dropdown ─────────────────────────────────────────────────────
        private ObservableCollection<MUnit> _units;
        public ObservableCollection<MUnit> Units
        {
            get => _units;
            set => SetProperty(ref _units, value);
        }

        private MUnit _selectedUnit;
        public MUnit SelectedUnit
        {
            get => _selectedUnit;
            set
            {
                if (SetProperty(ref _selectedUnit, value) && value != null)
                    MProduct.UnitId = value.Id;
            }
        }

        // ─── Constructor ───────────────────────────────────────────────────────
        public ProductViewModel()
        {
            _productService = new ProductService();
            _categoryService = new CategoryService();
            _subCategoryService = new SubCategoryService();
            _unitService = new UnitService();

            MProduct = new MProducts();
            FilteredSubCategories = new ObservableCollection<MSubCategory>();

            ProductSaveCommand = new RelayCommand(_ => Save());
            ProductDeleteCommand = new RelayCommand(_ => Delete(), _ => SelectedProduct != null);
            ProductResetCommand = new RelayCommand(_ => Reset());

            InitProductColumns();
            LoadDropdownData();
            LoadData();
            GenerateNextBarcode();   // pre-fill barcode for the first new product
        }

        // ─── LoadDropdownData ──────────────────────────────────────────────────
        private void LoadDropdownData()
        {
            Categories = new ObservableCollection<MCategory>(_categoryService.GetCategory());
            _allSubCategories = _subCategoryService.GetSubCategoryList();
            Units = new ObservableCollection<MUnit>(_unitService.GetUnit());
        }

        // ─── LoadData ──────────────────────────────────────────────────────────
        // ─── LoadData ──────────────────────────────────────────────────────────
        public void LoadData()
        {
            if (_allProducts != null)
            {
                foreach (var p in _allProducts)
                    p.PropertyChanged -= Product_PropertyChanged;
            }

            _allProducts = _productService.GetProductDisplay();
            Products = new ObservableCollection<ProductDisplayModel>(_allProducts);

            foreach (var p in _allProducts)
                p.PropertyChanged += Product_PropertyChanged;

            _checkedProducts.Clear();
            OnPropertyChanged(nameof(SelectedCountText));
            OnPropertyChanged(nameof(MultiSelectBarVisibility));
        }
        // ─── Selection tracking — driven by each row's checkbox ────────────────
        private void Product_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ProductDisplayModel.IsSelected)) return;

            _checkedProducts = _allProducts.Where(p => p.IsSelected).ToList();
            OnPropertyChanged(nameof(SelectedCountText));
            OnPropertyChanged(nameof(MultiSelectBarVisibility));
        }

        
        private void GenerateNextBarcode()
        {
            try
            {
                string lastBarcode = _productService.GetLastBarcode();
                string prefix = "M";
                long nextNumber = 1;

                if (!string.IsNullOrWhiteSpace(lastBarcode))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(lastBarcode, @"^(.*?)(\d+)$");
                    if (match.Success)
                    {
                        prefix = match.Groups[1].Value;
                        nextNumber = long.Parse(match.Groups[2].Value) + 1;
                    }
                }

                string generated = $"{prefix}{nextNumber}";
                _lastGeneratedBarcode = generated;
                BarcodeInput = generated;   // updates MProduct.Barcode + re-runs the filter (harmless: it won't match anything)
            }
            catch { /* DB unavailable – leave blank */ }
        }

        // ─── Save ──────────────────────────────────────────────────────────────
        private void Save()
        {
            if (string.IsNullOrWhiteSpace(MProduct.Barcode))
                MProduct.Barcode = $"M{_productService.GetProductCount() + 1}";

            // Never insert a second row for a barcode that already exists —
            // if one is found, treat this Save as an update to that record.
            if (MProduct.Id <= 0)
            {
                var existing = _productService.GetByBarcode(MProduct.Barcode);
                if (existing != null)
                    MProduct.Id = existing.Id;
            }

            // Default Category/SubCategory/Unit to first available if not selected
            if (MProduct.CategoryId <= 0)
                MProduct.CategoryId = Categories.FirstOrDefault()?.Id ?? 1;
            if (MProduct.SubCategoryId <= 0)
                MProduct.SubCategoryId = _allSubCategories.FirstOrDefault()?.Id ?? 1;
            if (MProduct.UnitId <= 0)
                MProduct.UnitId = Units.FirstOrDefault()?.Id ?? 1;

            bool success = MProduct.Id <= 0
                ? _productService.InsertProduct(MProduct)
                : _productService.UpdateProduct(MProduct);

            if (success)
            {
                bool qtyOk = _productService.SetProductQuantity(MProduct.Barcode, QuantityInput);
                if (!qtyOk)
                    MessageBox.Show("Product saved, but quantity update failed.", "Warning",
                        MessageBoxButton.OK, MessageBoxImage.Warning);

                LoadData();
                Reset();
            }
            else MessageBox.Show("Failed to save. Barcode may already exist.");
        }
        public ICommand UpdateQuantityCommand => new RelayCommand(p => UpdateQuantity(p as ProductDisplayModel));

        private void UpdateQuantity(ProductDisplayModel row)
        {
            if (row == null) return;

            bool ok = _productService.SetProductQuantity(row.Barcode, row.Quantity);
            if (ok)
                LoadData();   // refresh so the grid reflects the persisted value
            else
                MessageBox.Show($"Failed to update quantity for '{row.ProductName}'.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        // ─── Delete single ─────────────────────────────────────────────────────
        // ─── Delete (single OR multiple, depending on checkbox state) ──────────
        private void Delete()
        {
            // If any rows are checked via the checkbox column, treat this as a
            // bulk delete and ignore whichever single row happens to be selected.
            if (_checkedProducts.Any())
            {
                var result = MessageBox.Show(
                    $"Delete {_checkedProducts.Count} selected product(s)? This cannot be undone.",
                    "Confirm Bulk Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes) return;

                int deleted = 0;
                foreach (var p in _checkedProducts.ToList())
                {
                    if (_productService.DeleteProduct(p.Id)) deleted++;
                }

                MessageBox.Show($"{deleted} product(s) deleted.");
                LoadData();   // also clears _checkedProducts internally
                Reset();
                return;
            }

            // No checkboxes ticked — fall back to single-row delete
            if (SelectedProduct == null) return;

            var singleResult = MessageBox.Show(
                "Are you sure you want to delete this product?",
                "Confirm Delete", MessageBoxButton.YesNo);

            if (singleResult == MessageBoxResult.Yes)
            {
                if (_productService.DeleteProduct(SelectedProduct.Id))
                { LoadData(); Reset(); }
            }
        }

        // ─── Delete Selected (multi) ───────────────────────────────────────────
        private void DeleteSelected()
        {
            if (!_checkedProducts.Any()) return;

            var result = MessageBox.Show(
                $"Delete {_checkedProducts.Count} selected product(s)? This cannot be undone.",
                "Confirm Bulk Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            int deleted = 0;
            foreach (var p in _checkedProducts.ToList())
            {
                if (_productService.DeleteProduct(p.Id)) deleted++;
            }

            MessageBox.Show($"{deleted} product(s) deleted.");
            _checkedProducts.Clear();
            OnPropertyChanged(nameof(SelectedCountText));
            OnPropertyChanged(nameof(MultiSelectBarVisibility));
            LoadData();
            Reset();
        }

        // ─── Clear Selection ───────────────────────────────────────────────────
        private void ClearSelection()
        {
            _checkedProducts.Clear();
            OnPropertyChanged(nameof(SelectedCountText));
            OnPropertyChanged(nameof(MultiSelectBarVisibility));
        }

        // ─── Reset ─────────────────────────────────────────────────────────────
        private void Reset()
        {
            MProduct = new MProducts();
            SelectedProduct = null;
            SelectedCategory = null;
            SelectedSubCategory = null;
            SelectedUnit = null;
            QuantityInput = 0;                            // NEW
            FilteredSubCategories = new ObservableCollection<MSubCategory>();
            GenerateNextBarcode();
        }

        // ─── Export to Excel ───────────────────────────────────────────────────
        private void ExportToExcel()
        {
            if (Products == null || !Products.Any())
            {
                MessageBox.Show("No products to export.");
                return;
            }

            var sfd = new SaveFileDialog
            {
                Filter = "Excel Files|*.xlsx",
                FileName = $"Products_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            };
            if (sfd.ShowDialog() != true) return;

            try
            {
                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Products");

                // Only export visible columns (respect the column toggle)
                var visibleKeys = ProductColumns
                    .Where(c => c.IsVisible)
                    .Select(c => c.Key)
                    .ToList();

                // Header row
                int col = 1;
                foreach (var key in visibleKeys)
                {
                    var colOpt = ProductColumns.First(c => c.Key == key);
                    ws.Cell(1, col).Value = colOpt.Header;
                    ws.Cell(1, col).Style.Font.Bold = true;
                    ws.Cell(1, col).Style.Fill.BackgroundColor = XLColor.FromHtml("#1F3A5F");
                    ws.Cell(1, col).Style.Font.FontColor = XLColor.White;
                    col++;
                }

                // Data rows
                int row = 2;
                foreach (var p in Products)
                {
                    col = 1;
                    foreach (var key in visibleKeys)
                    {
                        object? val = key switch
                        {
                            "Barcode" => p.Barcode,
                            "ProductCode" => p.ProductCode,
                            "ProductName" => p.ProductName,
                            "CategoryName" => p.CategoryName,
                            "SubCategoryName" => p.SubCategoryName,
                            "PurchasePrice" => p.PurchasePrice,
                            "RetailSalePrice" => p.RetailSalePrice,
                            "MRP" => p.MRP,
                            "CGST" => p.CGST,
                            "SGST" => p.SGST,
                            "IGST" => p.IGST,
                            "UnitName" => p.UnitName,
                            "Size" => p.Size,
                            "Colour" => p.Colour,
                            "Rack" => p.Rack,
                            "HSNCode" => p.HSNCode,
                            _ => null
                        };
                        if (val != null) ws.Cell(row, col).Value = val.ToString();
                        col++;
                    }
                    row++;
                }

                // Auto-fit columns
                ws.Columns().AdjustToContents();

                // Freeze header row
                ws.SheetView.FreezeRows(1);

                wb.SaveAs(sfd.FileName);
                MessageBox.Show($"Exported {Products.Count} products successfully.", "Export Complete",
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Export failed: " + ex.Message);
            }
        }

        // ─── Import from Excel (unchanged logic) ───────────────────────────────
        private void ExecuteImportWizard()
        {
            var openFileDialog = new OpenFileDialog { Filter = "Excel Files|*.xls;*.xlsx;*.xlsm" };
            if (openFileDialog.ShowDialog() != true) return;

            try
            {
                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
                using var stream = File.Open(openFileDialog.FileName, FileMode.Open, FileAccess.Read);
                using var reader = ExcelReaderFactory.CreateReader(stream);
                var result = reader.AsDataSet();
                DataTable dt = result.Tables[0];

                var headers = new List<string> { "[ None ]" };
                foreach (DataColumn col in dt.Columns)
                {
                    var headerName = dt.Rows[0][col]?.ToString();
                    if (!string.IsNullOrEmpty(headerName)) headers.Add(headerName);
                }
                ExcelHeaders = new ObservableCollection<string>(headers);

                Mappings.Clear();
                var properties = typeof(MProducts).GetProperties();

                foreach (var prop in properties)
                {
                    if (prop.PropertyType.IsPrimitive ||
                        prop.PropertyType == typeof(string) ||
                        prop.PropertyType == typeof(decimal) ||
                        prop.PropertyType == typeof(double) ||
                        prop.PropertyType == typeof(DateTime) ||
                        prop.PropertyType == typeof(DateTime?) ||
                        prop.PropertyType == typeof(long))
                    {
                        if (prop.Name == "Id" || prop.Name.Contains("Date") || prop.Name.Contains("By")) continue;

                        var map = new ColumnMapping
                        {
                            DbPropertyName = prop.Name,
                            DisplayName = prop.Name,
                            SelectedExcelColumn = "[ None ]"
                        };

                        map.SelectedExcelColumn = ExcelHeaders.FirstOrDefault(h =>
                            h.Replace(" ", "").Replace("_", "").ToLower() ==
                            prop.Name.ToLower()) ?? "[ None ]";

                        Mappings.Add(map);
                    }
                }

                var mappingWin = new MyWPFCRUDApp.Views.ExcelMappingWindow(this);
                if (mappingWin.ShowDialog() == true)
                    ProcessExcelData(dt);
            }
            catch (Exception ex) { MessageBox.Show("Selection Error: " + ex.Message); }
        }

        private void ProcessExcelData(DataTable dt)
        {
            int successCount = 0;
            var productType = typeof(MProducts);

            for (int i = 1; i < dt.Rows.Count; i++)
            {
                var dr = dt.Rows[i];
                var p = new MProducts();

                foreach (var map in Mappings)
                {
                    if (string.IsNullOrEmpty(map.SelectedExcelColumn) || map.SelectedExcelColumn == "[ None ]")
                        continue;

                    int colIdx = ExcelHeaders.IndexOf(map.SelectedExcelColumn) - 1;
                    var val = dr[colIdx]?.ToString();
                    if (string.IsNullOrWhiteSpace(val)) continue;

                    var prop = productType.GetProperty(map.DbPropertyName);
                    if (prop != null && prop.CanWrite)
                    {
                        try
                        {
                            var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                            object convertedVal =
                                targetType == typeof(decimal) ? decimal.Parse(val) :
                                targetType == typeof(double) ? double.Parse(val) :
                                targetType == typeof(long) ? long.Parse(val) :
                                targetType == typeof(int) ? int.Parse(val) :
                                targetType == typeof(DateTime) ? DateTime.Parse(val) :
                                (object)val;
                            prop.SetValue(p, convertedVal);
                        }
                        catch { }
                    }
                }

                if (p.CategoryId == 0) p.CategoryId = Categories.FirstOrDefault()?.Id ?? 0;
                if (p.SubCategoryId == 0) p.SubCategoryId = _allSubCategories.FirstOrDefault()?.Id ?? 0;
                if (p.UnitId == 0) p.UnitId = Units.FirstOrDefault()?.Id ?? 0;

                if (!string.IsNullOrEmpty(p.ProductName) && !string.IsNullOrEmpty(p.Barcode))
                    if (_productService.InsertProduct(p)) successCount++;
            }

            MessageBox.Show($"{successCount} products imported successfully.");
            LoadData();
        }
    }
}