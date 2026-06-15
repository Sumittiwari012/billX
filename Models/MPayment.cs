using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyWPFCRUDApp.Models
{
    public class MPayment:BaseEntity
    {
        public long SupplierId { get; set; }    
        public string InvoiceNumber { get; set; } // The Bill No. from the supplier
        public string PaymentMethod { get; set; } // Cash, Credit, Online

        public string BankAccountNumber { get; set; } // Optional, for online payments
        public decimal AmountPaid { get; set; }
    }
}
