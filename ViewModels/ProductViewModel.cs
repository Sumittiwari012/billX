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
                    // Map display model into MProduct for editing
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

                    // Set Category first — its setter filters SubCategories automatically
                    SelectedCategory = Categories.FirstOrDefault(c => c.Id == value.CategoryId);
                    // Now FilteredSubCategories is populated, safe to set SubCategory
                    SelectedSubCategory = FilteredSubCategories.FirstOrDefault(s => s.Id == value.SubCategoryId);
                    SelectedUnit = Units.FirstOrDefault(u => u.Id == value.UnitId);
                }
            }
        }

        // ─── Form Model ────────────────────────────────────────────────────────
        private MProducts _mProduct;
        public MProducts MProduct
        {
            get => _mProduct;
            set => SetProperty(ref _mProduct, value);
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
                        // Filter subcategories for selected category
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

        // ─── SubCategory Dropdown (filtered by selected category) ──────────────
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

            LoadDropdownData();
            LoadData();
        }

        // ─── LoadDropdownData ──────────────────────────────────────────────────
        private void LoadDropdownData()
        {
            Categories = new ObservableCollection<MCategory>(_categoryService.GetCategory());
            _allSubCategories = _subCategoryService.GetSubCategoryList();
            Units = new ObservableCollection<MUnit>(_unitService.GetUnit());
        }

        // ─── LoadData ──────────────────────────────────────────────────────────
        public void LoadData()
        {
            var data = _productService.GetProductDisplay();
            Products = new ObservableCollection<ProductDisplayModel>(data);
        }

        // ─── Save ──────────────────────────────────────────────────────────────
        private void Save()
        {
            if (string.IsNullOrWhiteSpace(MProduct.ProductName))
            {
                System.Windows.MessageBox.Show("Product Name is required!");
                return;
            }
            if (string.IsNullOrWhiteSpace(MProduct.Barcode))
            {
                System.Windows.MessageBox.Show("Barcode is required!");
                return;
            }
            if (MProduct.CategoryId <= 0)
            {
                System.Windows.MessageBox.Show("Please select a Category!");
                return;
            }
            if (MProduct.SubCategoryId <= 0)
            {
                System.Windows.MessageBox.Show("Please select a Sub Category!");
                return;
            }
            if (MProduct.UnitId <= 0)
            {
                System.Windows.MessageBox.Show("Please select a Unit!");
                return;
            }

            bool success;
            if (MProduct.Id <= 0)
                success = _productService.InsertProduct(MProduct);
            else
                success = _productService.UpdateProduct(MProduct);

            if (success)
            {
                LoadData();
                Reset();
            }
            else
            {
                System.Windows.MessageBox.Show("Failed to save. Barcode may already exist.");
            }
        }

        // ─── Delete ────────────────────────────────────────────────────────────
        private void Delete()
        {
            var result = System.Windows.MessageBox.Show(
                "Are you sure you want to delete this product?",
                "Confirm Delete",
                System.Windows.MessageBoxButton.YesNo);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                if (_productService.DeleteProduct(SelectedProduct.Id))
                {
                    LoadData();
                    Reset();
                }
            }
        }

        // ─── Reset ─────────────────────────────────────────────────────────────
        private void Reset()
        {
            MProduct = new MProducts();
            SelectedProduct = null;
            SelectedCategory = null;
            SelectedSubCategory = null;
            SelectedUnit = null;
            FilteredSubCategories = new ObservableCollection<MSubCategory>();
        }
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

                // 1. Setup Excel Headers with the "None" option
                var headers = new List<string> { "[ None ]" };
                foreach (DataColumn col in dt.Columns)
                {
                    var headerName = dt.Rows[0][col]?.ToString();
                    if (!string.IsNullOrEmpty(headerName)) headers.Add(headerName);
                }
                ExcelHeaders = new ObservableCollection<string>(headers);

                // 2. DYNAMIC MAPPING: Scan MProducts for all properties
                Mappings.Clear();
                var properties = typeof(MProducts).GetProperties();

                foreach (var prop in properties)
                {
                    // Skip complex objects (like MCategory, MSubCategory) and only map basic types
                    if (prop.PropertyType.IsPrimitive ||
                        prop.PropertyType == typeof(string) ||
                        prop.PropertyType == typeof(decimal) ||
                        prop.PropertyType == typeof(double) ||
                        prop.PropertyType == typeof(DateTime) ||
                        prop.PropertyType == typeof(DateTime?) ||
                        prop.PropertyType == typeof(long))
                    {
                        // Ignore BaseEntity fields like CreatedBy, ModifiedBy
                        if (prop.Name == "Id" || prop.Name.Contains("Date") || prop.Name.Contains("By")) continue;

                        var map = new ColumnMapping
                        {
                            DbPropertyName = prop.Name,
                            DisplayName = prop.Name, // This will be the nomenclature used in your C# class
                            SelectedExcelColumn = "[ None ]"
                        };

                        // Smart Auto-Mapping: Match nomenclature even if casing/spacing differs
                        map.SelectedExcelColumn = ExcelHeaders.FirstOrDefault(h =>
                            h.Replace(" ", "").Replace("_", "").ToLower() ==
                            prop.Name.ToLower()) ?? "[ None ]";

                        Mappings.Add(map);
                    }
                }

                // 3. Open Mapping Window
                var mappingWin = new MyWPFCRUDApp.Views.ExcelMappingWindow(this);
                if (mappingWin.ShowDialog() == true)
                {
                    ProcessExcelData(dt);
                }
            }
            catch (Exception ex) { MessageBox.Show("Selection Error: " + ex.Message); }
        }

        private void ProcessExcelData(DataTable dt)
        {
            int successCount = 0;
            var productType = typeof(MProducts);

            // Skip row 0 (headers)
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

                    // Find the property by the nomenclature defined in the mapping
                    var prop = productType.GetProperty(map.DbPropertyName);
                    if (prop != null && prop.CanWrite)
                    {
                        try
                        {
                            // Handle type conversions dynamically based on the Property Type
                            object convertedVal = null;
                            var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

                            if (targetType == typeof(decimal)) convertedVal = decimal.Parse(val);
                            else if (targetType == typeof(double)) convertedVal = double.Parse(val);
                            else if (targetType == typeof(long)) convertedVal = long.Parse(val);
                            else if (targetType == typeof(int)) convertedVal = int.Parse(val);
                            else if (targetType == typeof(DateTime)) convertedVal = DateTime.Parse(val);
                            else convertedVal = val;

                            prop.SetValue(p, convertedVal);
                        }
                        catch { /* Log conversion error for specific cell */ }
                    }
                }

                // Final Safety: Ensure required IDs are set
                if (p.CategoryId == 0) p.CategoryId = Categories.FirstOrDefault()?.Id ?? 0;
                if (p.SubCategoryId == 0) p.SubCategoryId = _allSubCategories.FirstOrDefault()?.Id ?? 0;
                if (p.UnitId == 0) p.UnitId = Units.FirstOrDefault()?.Id ?? 0;

                // Save
                if (!string.IsNullOrEmpty(p.ProductName) && !string.IsNullOrEmpty(p.Barcode))
                {
                    if (_productService.InsertProduct(p)) successCount++;
                }
            }

            MessageBox.Show($"{successCount} products imported successfully.");
            LoadData();
        }
    }
}