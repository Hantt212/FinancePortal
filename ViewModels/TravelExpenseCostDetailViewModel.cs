using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FinancePortal.ViewModels
{
    public class TravelExpenseCostDetailViewModel
    {
        public int CostBudgetID { get; set; }
        public int BudgetID {  get; set; }
        public string BudgetName { get; set; }
        public long BudgetAmountAtSubmit { get; set; }
        public long BudgetRemainAtSubmit { get; set; }
        public long BudgetUsedAtSubmit { get; set; }
        public string CostName { get; set; }
        public int CostAmount { get; set; }
    }
}