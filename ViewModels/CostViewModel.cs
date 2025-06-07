using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FinancePortal.ViewModels
{
    public class CostViewModel
    {
        public int CostID { get; set; }
        public string CostName { get; set; }
        public bool Checked { get; set; }
    }
}