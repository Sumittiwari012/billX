using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyWPFCRUDApp.Models
{
    public class MUnit:BaseEntity
    {
        [Required]
        [StringLength(50)]
        public string UnitName { get; set; } // e.g., Kg, Pcs, Box
    }
}
