using MyWPFCRUDApp.ViewModels;
using System.Collections.Generic;

namespace MyWPFCRUDApp.Models
{
    public class ScannedBillResult
    {
        public string InvoiceNumber { get; set; } = "";
        public string InvoiceDate   { get; set; } = "";
        public string SupplierName  { get; set; } = "";
        public decimal GrandTotal   { get; set; }
        public List<ScannedBillItem> Items { get; set; } = new();
    }

    public class ScannedBillItem : BaseViewModel
    {
        private string _description = "";
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        private double _quantity;
        public double Quantity
        {
            get => _quantity;
            set
            {
                if (SetProperty(ref _quantity, value))
                    RecalcAmount();
            }
        }

        private decimal _purchasePrice;
        public decimal PurchasePrice
        {
            get => _purchasePrice;
            set
            {
                if (SetProperty(ref _purchasePrice, value))
                    RecalcAmount();
            }
        }

        private decimal _wholesalePrice;
        public decimal WholesalePrice
        {
            get => _wholesalePrice;
            set => SetProperty(ref _wholesalePrice, value);
        }

        private decimal _mrp;
        public decimal MRP
        {
            get => _mrp;
            set => SetProperty(ref _mrp, value);
        }

        private decimal _amount;
        public decimal Amount
        {
            get => _amount;
            set => SetProperty(ref _amount, value);
        }

        private long _matchedProductId;
        public long MatchedProductId
        {
            get => _matchedProductId;
            set => SetProperty(ref _matchedProductId, value);
        }

        private string _matchedProductName = "— not matched —";
        public string MatchedProductName
        {
            get => _matchedProductName;
            set => SetProperty(ref _matchedProductName, value);
        }

        private void RecalcAmount()
        {
            if (_quantity > 0 && _purchasePrice > 0)
                Amount = (decimal)_quantity * _purchasePrice;
        }
    }
}
