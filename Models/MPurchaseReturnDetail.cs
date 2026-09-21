// Models/MPurchaseReturnDetail.cs
using System;

namespace MyWPFCRUDApp.Models
{
    public class MPurchaseReturnDetail
    {
        public long Id { get; set; }
        public long ProductId { get; set; }
        public string? ProductName { get; set; }   // populated on read
        public string? Barcode { get; set; }        // populated on read

        public double Quantity { get; set; }
        public decimal PurchasePrice { get; set; }

        public string? Batch { get; set; }
        public DateTime? MfgDate { get; set; }
        public DateTime? ExpDate { get; set; }
        public string? Reason { get; set; }
    }
}