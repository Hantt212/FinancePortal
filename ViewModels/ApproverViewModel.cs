using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FinancePortal.ViewModels
{
    public class ApproverViewModel
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Position { get; set; }
        public string Signature { get; set; }
        public DateTime? SignDate { get; set; }
    }
}