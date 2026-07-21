using System;
using System.ComponentModel.DataAnnotations;

namespace MyWPFCRUDApp.Models
{
    public class MLoginLogout : BaseEntity
    {
        [Required]
        public long CounterId { get; set; }

        [Required]
        public long UserId { get; set; }

        public DateTime LoginTime { get; set; }
        public DateTime? LogoutTime { get; set; }
        public bool Settlement { get; set; }

        // Display-only, populated via join — not mapped back on save
        public string CounterName { get; set; }
        public string UserName { get; set; }
    }
}