using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyWPFCRUDApp.Models
{
    public class MPaymentMethod:BaseEntity
    {
        [Required]
        [StringLength(50)]
        public string PaymentMethod { get; set; } // e.g., Kg, Pcs, Box
    }
}
