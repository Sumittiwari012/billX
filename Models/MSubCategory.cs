using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyWPFCRUDApp.Models
{
    public class MSubCategory:BaseEntity
    {
        [Required]
        [StringLength(100)]
        public string SubCategoryName { get; set; }

        // Foreign Key for Category
        public long CategoryId { get; set; }
        [ForeignKey("CategoryId")]
        public MCategory MCategory { get; set; }
    }
}
