using System;

namespace MyWPFCRUDApp.Models
{
    public class MPettyCash: BaseEntity
    {
        public long Id { get; set; }
        public decimal PettyCash { get; set; }
        public long CounterId { get; set; }
        public DateTime? Date { get; set; }
        public bool Accept { get; set; }

        // Display-only, populated via JOIN in GetPettyCash(); not written back to DB directly
        public string CounterName { get; set; }
    }
}