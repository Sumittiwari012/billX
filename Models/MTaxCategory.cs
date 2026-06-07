using MyWPFCRUDApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WPFCRUDApp.Models
{
    public class MTaxCategory: BaseEntity
    {
        public decimal CGST { get; set; }

        public decimal SGST { get; set; }

        public decimal IGST { get; set; }
    }
}
