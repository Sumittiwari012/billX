using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks; 
namespace MyWPFCRUDApp.Models
{
    public class MCategory: BaseEntity
    {
        [Required]
        [StringLength(100)]
        public string CategoryName { get; set; }

    }
}
