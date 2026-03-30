using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using WPFCRUDApp.Models;

namespace MyWPFCRUDApp.Models
{
    public class MPurchaseMaster
    {
        public string InvoiceNumber { get; set; } // The Bill No. from the supplier
        public long SupplierId { get; set; }
        public DateTime PurchaseDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Discount { get; set; }

        public string PaymentMode { get; set; } // Cash, Credit, Online
        public string Remarks { get; set; }
        public virtual ICollection<MPurchaseDetail> MPurchaseDetail { get; set; }

        [ForeignKey(nameof(SupplierId))]
        [JsonIgnore] // Stops the API from requiring the full Category object
        public virtual MSupplier? MSupplier { get; set; }
    }
}
