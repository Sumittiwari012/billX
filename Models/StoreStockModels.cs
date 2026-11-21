using System;

namespace MyWPFCRUDApp.Models
{
    /// <summary>Read-only row: one product's stock in one store.</summary>
    public class StoreStockRow
    {
        public long Id { get; set; }
        public long StoreId { get; set; }
        public string StoreName { get; set; }
        public long ProductId { get; set; }
        public string Barcode { get; set; }
        public string ProductCode { get; set; }
        public string ProductName { get; set; }
        public string UnitName { get; set; }
        public decimal Quantity { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }

    /// <summary>Read-only row: a stock transfer that involves at least one Store.</summary>
    public class StoreTransferRow
    {
        public long Id { get; set; }
        public string TransferNo { get; set; }
        public string FromType { get; set; }
        public long FromId { get; set; }
        public string FromName { get; set; }
        public string ToType { get; set; }
        public long ToId { get; set; }
        public string ToName { get; set; }
        public string Status { get; set; }
        public string Remarks { get; set; }
        public string DispatchedBy { get; set; }
        public DateTime? DispatchedAt { get; set; }
        public string ReceivedBy { get; set; }
        public DateTime? ReceivedAt { get; set; }
        public DateTime? CreatedDate { get; set; }

        public string FromDisplay => $"{FromType}: {FromName}";
        public string ToDisplay => $"{ToType}: {ToName}";
    }

    /// <summary>Read-only row: one line of a stock transfer.</summary>
    public class StoreTransferItemRow
    {
        public long Id { get; set; }
        public long TransferId { get; set; }
        public long ProductId { get; set; }
        public string Barcode { get; set; }
        public string ProductName { get; set; }
        public decimal DispatchedQty { get; set; }
        public decimal? ReceivedQty { get; set; }
        public string Remarks { get; set; }
    }
}
