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
        public string CategoryName { get; set; }

        public int TaxPercentage { get; set; }
    }
}
