// Models/MPurchaseReturnMaster.cs
using System;
using System.Collections.Generic;

namespace MyWPFCRUDApp.Models
{
    public class MPurchaseReturnMaster
    {
        public long Id { get; set; }
        public string ReturnInvoiceNumber { get; set; } = "";
        public string InvoiceNumber { get; set; } = "";   // original purchase invoice
        public long SupplierId { get; set; }
        public string? SupplierName { get; set; }          // populated on read, not persisted here
        public DateTime ReturnDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string? Remarks { get; set; }

        public List<MPurchaseReturnDetail> MPurchaseReturnDetail { get; set; } = new();
    }
}