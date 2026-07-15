using System.ComponentModel.DataAnnotations.Schema;

namespace MyWPFCRUDApp.Models
{
    public class ProductQuantity:BaseEntity
    {
        public string ProductCode { get; set; }
        public string Barcode { get; set; }

        public long MinimumSellingQuantity { get; set; } = 1;

        public long Quantity { get; set; } = 0;

        


    }
}
