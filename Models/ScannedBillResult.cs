using MyWPFCRUDApp.ViewModels;
using System.Collections.Generic;
using System.Linq;

namespace MyWPFCRUDApp.Models
{
    public class ScannedBillResult : BaseViewModel
    {
        public string InvoiceNumber { get; set; } = "";
        public string InvoiceDate { get; set; } = "";
        public string SupplierName { get; set; } = "";

        private decimal _grandTotal;
        public decimal GrandTotal
        {
            get => _grandTotal;
            set => SetProperty(ref _grandTotal, value);
        }

        public List<ScannedBillItem> Items { get; set; } = new();

        // NEW — true when the AI's response was cut off (finish_reason ==
        // "length") before it finished extracting every row, even though the
        // partial JSON it did emit happened to be well-formed. The caller
        // (PurchaseViewModel.ExecuteScanBillAsync) checks this after a
        // successful scan to warn the user that Items.Count may be less
        // than the bill's real item count, instead of failing outright and
        // discarding whatever WAS successfully extracted.
        public bool WasTruncated { get; set; }

        public void RecalculateGrandTotal()
        {
            GrandTotal = Items.Sum(i => i.Amount);
        }
    }

    public class ScannedBillItem : BaseViewModel
    {
        private string _description = "";
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        // HSN/SAC code — filled by the AI scan when confident, blank otherwise.
        // Always editable here since it's a best-effort guess, not authoritative.
        private string _hsnCode = "";
        public string HsnCode
        {
            get => _hsnCode;
            set => SetProperty(ref _hsnCode, value);
        }

        // ── NEW — extra columns, same idea as the Excel import ──────────────
        private string _size = "";
        public string Size
        {
            get => _size;
            set => SetProperty(ref _size, value);
        }

        private string _colour = "";
        public string Colour
        {
            get => _colour;
            set => SetProperty(ref _colour, value);
        }

        private decimal _cgst;
        public decimal CGST
        {
            get => _cgst;
            set => SetProperty(ref _cgst, value);
        }

        private decimal _sgst;
        public decimal SGST
        {
            get => _sgst;
            set => SetProperty(ref _sgst, value);
        }

        private decimal _igst;
        public decimal IGST
        {
            get => _igst;
            set => SetProperty(ref _igst, value);
        }
        // ─────────────────────────────────────────────────────────────────

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
        private decimal _retailPrice;
        public decimal RetailPrice
        {
            get => _retailPrice;
            set => SetProperty(ref _retailPrice, value);
        }
        private decimal _mrp;
        public decimal MRP
        {
            get => _mrp;
            set => SetProperty(ref _mrp, value);
        }

        // ══════════════════════════════════════════════════════════════════
        // NEW — "came from the scan itself" flags.
        //
        // Set ONLY by BillScanService.ParseResponse(), when the AI actually
        // extracted a non-zero MRP / Retail / Wholesale price printed on the
        // bill. When false, that field's value of 0 just means "the bill
        // didn't print this — calculate it from the % markup boxes instead."
        //
        // BillScanReviewWindow.RecalculatePrices() checks these flags before
        // overwriting a field: a real scanned value is left alone even when
        // Purchase Price or the %-markup boxes change afterward. A value the
        // user edits by hand in the review grid is NOT re-flagged here — the
        // user is the final authority in that screen, and RecalculatePrices()
        // itself is the only place these fields get silently recalculated.
        // ══════════════════════════════════════════════════════════════════
        public bool MrpFromScan { get; set; }
        public bool RetailPriceFromScan { get; set; }
        public bool WholesalePriceFromScan { get; set; }

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