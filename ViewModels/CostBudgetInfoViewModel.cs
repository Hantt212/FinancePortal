using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FinancePortal.ViewModels
{
    public class CostBudgetInfoViewModel
    {
        public int ID { get; set; }
        public int CostID { get; set; }
        public string CostName { get; set; }
        public int BudgetID { get; set; }
        public string BudgetName { get; set; }
    }

}