using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FinancePortal.ViewModels
{
	public class TravelExpenseBudgetViewModel
	{
        public int ID { get; set; }
        public string BudgetName { get; set; }
        public decimal BudgetAmount { get; set; }
        public decimal BudgetUsed { get; set; }
        public decimal BudgetRemaining { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public string CreatedBy { get; set; }
        public string UpdatedBy { get; set; }
        public bool IsShown { get; set; }
    }
}