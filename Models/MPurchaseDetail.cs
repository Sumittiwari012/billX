using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyWPFCRUDApp.Models
{
    public class MPurchaseDetail
    {
        public long PurchaseMasterId { get; set; }
        [ForeignKey(nameof(PurchaseMasterId))]
        public virtual MPurchaseMaster? PurchaseMaster { get; set; }

        public long ProductId { get; set; }
        [ForeignKey(nameof(ProductId))]
        public virtual MProducts? Product { get; set; }

        public double Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PurchasePrice { get; set; } // Price at time of purchase

        [Column(TypeName = "decimal(18,2)")]
        public decimal AfterTaxation { get; set; }
    }
}
