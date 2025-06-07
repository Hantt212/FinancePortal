using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FinancePortal.ViewModels
{
    public class BudgetViewModel
    {
        public int BudgetID { get; set; }
        public string BudgetName { get; set; }
        public long BudgetAmount { get; set; }
        public long BudgetUsed { get; set; }
        public long BudgetRemaining { get; set; }
        public List<int> CostIDList { get; set; }
    }
}