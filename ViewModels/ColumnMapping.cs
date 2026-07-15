using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyWPFCRUDApp.ViewModels
{
    public class ColumnMapping
    {
        public string DbPropertyName { get; set; } // e.g., "ProductName"
        public string DisplayName { get; set; }    // e.g., "Product Name"
        public string SelectedExcelColumn { get; set; } // User's choice from Excel
    }
}
