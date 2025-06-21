using FinancePortal.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FinancePortal.ViewModels
{
    public class PaymentViewModel
    {
        public List<PaymentsModel> Payments { get; set; }
        public List<BudgetViewModel> Budgets { get; set; }
        public List<CostBudgetInfoViewModel> Costs { get; set; }

        public long CashReceived {  get; set; }
    }
}