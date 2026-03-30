using MyWPFCRUDApp.Helpers;
using MyWPFCRUDApp.Models;
using MyWPFCRUDApp.Services;
using MyWPFCRUDApp.Views;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using WPFCRUDApp.Models;

namespace MyWPFCRUDApp.ViewModels
{
    public class PurchaseViewModel : BaseViewModel
    {
        // --- Services ---
        private readonly PurchaseService _purchaseService;
        private readonly SupplierService _supplierService;
        private readonly ProductService _productService;

        // --- Commands ---
        public ICommand AddItemCommand { get; }
        public ICommand PurchaseDeleteCommand { get; }
        public ICommand PurchaseSaveCommand { get; }
        public ICommand PurchaseResetCommand { get; }
        public ICommand BarcodeSearchCommand { get; }
        public ICommand OpenAddSupplierCommand { get; }

        // --- Collections ---
        public ObservableCollection<MSupplier> Suppliers { get; set; }
        public ObservableCollection<MProducts> Products { get; set; }
        public ObservableCollection<MPurchaseDetail> PurchaseItems { get; set; }

        // --- Form Models ---
        private MPurchaseMaster _purchaseMaster;
        public MPurchaseMaster PurchaseMaster
        {
            get => _purchaseMaster;
            set => SetProperty(ref _purchaseMaster, value);
        }

        private MPurchaseDetail _newItem;
        public MPurchaseDetail NewItem // Current item being configured in the UI
        {
            get => _newItem;
            set => SetProperty(ref _newItem, value);
        }

        // --- Selected Properties ---
        private MSupplier _selectedSupplier;
        public MSupplier SelectedSupplier
        {
            get => _selectedSupplier;
            set
            {
                if (SetProperty(ref _selectedSupplier, value) && value != null)
                {
                    PurchaseMaster.SupplierId = value.Id;
                }
            }
        }

        private MProducts _selectedProduct;
        public MProducts SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                if (SetProperty(ref _selectedProduct, value) && value != null)
                {
                    NewItem.ProductId = value.Id;
                    NewItem.PurchasePrice = value.PurchasePrice; // Auto-fill current price
                    OnPropertyChanged(nameof(NewItem));
                }
            }
        }

        public PurchaseViewModel()
        {
            _purchaseService = new PurchaseService();
            _supplierService = new SupplierService();
            _productService = new ProductService();

            AddItemCommand = new RelayCommand(_ => AddItemToGrid());
            PurchaseDeleteCommand = new RelayCommand(p => RemoveItemFromGrid(p as MPurchaseDetail));
            PurchaseSaveCommand = new RelayCommand(_ => SavePurchase());
            PurchaseResetCommand = new RelayCommand(_ => ResetForm());
            BarcodeSearchCommand = new RelayCommand(p => HandleBarcodeSearch(p?.ToString()));
            OpenAddSupplierCommand = new RelayCommand(_ => OpenSupplierWindow());
            InitializeData();
        }
        // Replace/Update these methods in PurchaseViewModel.cs

        // Inside PurchaseViewModel.cs

        // Replace these methods in your PurchaseViewModel.cs

        private void HandleBarcodeSearch(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return;

            // 1. Search in the local list (loaded from DB during InitializeData)
            var product = Products.FirstOrDefault(p => p.Barcode == barcode || p.ProductCode == barcode);

            if (product != null)
            {
                // 2. Product exists -> Add to cart or increment quantity
                AddToCart(product);
            }
            else
            {
                // 3. Product not found -> Open Addition Window
                var result = MessageBox.Show($"Barcode '{barcode}' not found. Create new product?",
                                             "Product Missing", MessageBoxButton.YesNo);
                if (result == MessageBoxResult.Yes)
                {
                    var addWin = new AddProductWindow(barcode); // Pass barcode to window
                    addWin.Owner = Application.Current.MainWindow;
                    if (addWin.ShowDialog() == true)
                    {
                        // Reload products from DB to get the new one
                        Products = new ObservableCollection<MProducts>(_productService.GetProducts());
                        var newProd = Products.FirstOrDefault(p => p.Barcode == barcode);
                        if (newProd != null) AddToCart(newProd);
                    }
                }
            }
        }

        private void AddToCart(MProducts product)
        {
            var existingItem = PurchaseItems.FirstOrDefault(i => i.ProductId == product.Id);

            if (existingItem != null)
            {
                int index = PurchaseItems.IndexOf(existingItem);

                existingItem.Quantity += 1;

                decimal taxRate = (decimal)(product.CGST + product.SGST + product.CESS);
                decimal subtotal = (decimal)existingItem.Quantity * existingItem.PurchasePrice;

                existingItem.AfterTaxation = subtotal + (subtotal * taxRate / 100);

                // FORCE UI REFRESH
                PurchaseItems.RemoveAt(index);
                PurchaseItems.Insert(index, existingItem);
            }
            else
            {
                decimal taxRate = (decimal)(product.CGST + product.SGST + product.CESS);

                PurchaseItems.Add(new MPurchaseDetail
                {
                    ProductId = product.Id,
                    Product = product,
                    Quantity = 1,
                    PurchasePrice = product.PurchasePrice,
                    AfterTaxation = product.PurchasePrice + (product.PurchasePrice * taxRate / 100)
                });
            }

            CalculateTotal();
        }


        private void OpenSupplierWindow()
        {
            var win = new AddSupplierWindow();
            win.Owner = Application.Current.MainWindow;
            if (win.ShowDialog() == true)
            {
                // Refresh list and select the new one
                Suppliers = new ObservableCollection<MSupplier>(_supplierService.GetAllSuppliers());
                OnPropertyChanged(nameof(Suppliers));
                SelectedSupplier = Suppliers.FirstOrDefault(s => s.SupplierName == win.NewSupplier.SupplierName);
            }
        }

        private void InitializeData()
        {
            PurchaseMaster = new MPurchaseMaster { PurchaseDate = DateTime.Now };
            NewItem = new MPurchaseDetail();
            PurchaseItems = new ObservableCollection<MPurchaseDetail>();

            Suppliers = new ObservableCollection<MSupplier>(_supplierService.GetAllSuppliers());
            Products = new ObservableCollection<MProducts>(_productService.GetProducts());
        }

        private void AddItemToGrid()
        {
            if (SelectedProduct == null || NewItem.Quantity <= 0)
            {
                MessageBox.Show("Please select a product and enter a valid quantity.");
                return;
            }

            AddToCart(SelectedProduct);

            NewItem = new MPurchaseDetail();
            SelectedProduct = null;
        }

        private void RemoveItemFromGrid(MPurchaseDetail item)
        {
            if (item != null && PurchaseItems.Contains(item))
            {
                PurchaseItems.Remove(item);
                CalculateTotal(); // Updates Grand Total after removal
            }
        }

        private void CalculateTotal()
        {
            decimal grossTotal = PurchaseItems.Sum(x => x.AfterTaxation);
            PurchaseMaster.TotalAmount = grossTotal - PurchaseMaster.Discount;
            OnPropertyChanged(nameof(PurchaseMaster));
        }

        private void SavePurchase()
        {
            if (SelectedSupplier == null)
            {
                MessageBox.Show("Please select a supplier.");
                return;
            }
            if (!PurchaseItems.Any())
            {
                MessageBox.Show("Please add at least one item.");
                return;
            }

            // Assign the list to the master model
            PurchaseMaster.MPurchaseDetail = PurchaseItems.ToList();

            if (_purchaseService.AddPurchase(PurchaseMaster))
            {
                MessageBox.Show("Purchase recorded and stock updated successfully!");
                ResetForm();
            }
            else
            {
                MessageBox.Show("Error occurred while saving the purchase.");
            }
        }

        private void ResetForm()
        {
            InitializeData();
            SelectedSupplier = null;
        }
    }
}