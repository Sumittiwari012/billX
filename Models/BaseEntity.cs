using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyWPFCRUDApp.Models
{
    public class BaseEntity
    {
        public long Id { get; set; }
        public string createdBy { get; set; } = "System";
        public DateTime createdDate { get; set; } = DateTime.Now;
        public string modifiedBy { get; set; } = "System"; // Default prevents the MySQL error
        public DateTime modifiedDate { get; set; } = DateTime.Now;
    }
}
