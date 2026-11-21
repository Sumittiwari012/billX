using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MyWPFCRUDApp.Models
{
    public static class LocationType
    {
        public const string Godown = "Godown";
        public const string Store = "Store";
    }

    public static class TransferStatus
    {
        public const string Draft = "Draft";
        public const string Dispatched = "Dispatched";
        public const string Received = "Received";
        public const string PartiallyReceived = "PartiallyReceived";
        public const string Closed = "Closed";
        public const string Rejected = "Rejected";
        public const string Cancelled = "Cancelled";
    }

    public static class MovementType
    {
        public const string OpeningStock = "opening_stock";
        public const string Adjustment = "adjustment";
        public const string TransferOut = "transfer_out";
        public const string TransferIn = "transfer_in";
        public const string CancelReturn = "cancel_return";
        public const string RejectReturn = "reject_return";
        public const string ShortageReturn = "shortage_return";
    }

    public class LocationOption
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
    }

    public class StockRow
    {
        public long Id { get; set; }
        public long LocationId { get; set; }
        public string LocationName { get; set; }
        public long ProductId { get; set; }
        public string Barcode { get; set; }
        public string ProductName { get; set; }
        public decimal Quantity { get; set; }
        public DateTime ModifiedDate { get; set; }
    }

    public class LedgerRow
    {
        public long Id { get; set; }
        public string Barcode { get; set; }
        public string ProductName { get; set; }
        public string LocationType { get; set; }
        public long LocationId { get; set; }
        public string LocationName { get; set; }
        public decimal QtyChange { get; set; }
        public decimal BalanceAfter { get; set; }
        public string MovementType { get; set; }
        public string ReferenceType { get; set; }
        public long? ReferenceId { get; set; }
        public string Remarks { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class StockTransfer
    {
        public long Id { get; set; }
        public string TransferNo { get; set; }
        public string FromType { get; set; } = LocationType.Godown;
        public long FromId { get; set; }
        public string FromName { get; set; }
        public string ToType { get; set; } = LocationType.Store;
        public long ToId { get; set; }
        public string ToName { get; set; }
        public string Status { get; set; } = TransferStatus.Draft;
        public string Remarks { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string DispatchedBy { get; set; }
        public DateTime? DispatchedAt { get; set; }
        public string ReceivedBy { get; set; }
        public DateTime? ReceivedAt { get; set; }
        public int ItemCount { get; set; }

        public string FromDisplay => $"{FromType}: {FromName}";
        public string ToDisplay => $"{ToType}: {ToName}";
    }

    public class StockTransferItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        decimal _dispatched;
        decimal? _received;
        string _remarks;

        public long Id { get; set; }
        public long TransferId { get; set; }
        public long ProductId { get; set; }
        public string Barcode { get; set; }
        public string ProductName { get; set; }
        /// <summary>Current quantity at the source (display only).</summary>
        public decimal SourceQty { get; set; }

        public decimal DispatchedQty
        {
            get => _dispatched;
            set { _dispatched = value; Raise(); Raise(nameof(Shortage)); }
        }

        public decimal? ReceivedQty
        {
            get => _received;
            set { _received = value; Raise(); Raise(nameof(Shortage)); }
        }

        public string Remarks
        {
            get => _remarks;
            set { _remarks = value; Raise(); }
        }

        public decimal Shortage => _received.HasValue ? _dispatched - _received.Value : 0;
    }
}
