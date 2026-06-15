using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyWPFCRUDApp.Models
{
    public class MBankAccountMaster:BaseEntity
    {
       
        [StringLength(50)]
        public string AccountNumber { get; set; }

    }
}
