using MyWPFCRUDApp.Helpers;
using MyWPFCRUDApp.Models;
using MyWPFCRUDApp.Services;
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

            InitializeData();
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

            // Calculate AfterTaxation if needed (Simplified logic)
            NewItem.AfterTaxation = NewItem.PurchasePrice * (decimal)NewItem.Quantity;
            NewItem.Product = SelectedProduct; // For display in DataGrid

            PurchaseItems.Add(NewItem);

            // Recalculate Master Total
            CalculateTotal();

            // Reset Item Entry Form
            NewItem = new MPurchaseDetail();
            SelectedProduct = null;
        }

        private void RemoveItemFromGrid(MPurchaseDetail item)
        {
            if (item != null)
            {
                PurchaseItems.Remove(item);
                CalculateTotal();
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