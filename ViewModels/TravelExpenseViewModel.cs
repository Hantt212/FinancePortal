using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FinancePortal.ViewModels
{
    public class TravelExpenseViewModel
    {
        public int ID { get; set; }
        public string TarNo { get; set; }
        public string TripPurpose { get; set; }
        public DateTime RequestDate { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int StatusID { get; set; }
        public int TripDays { get; set; }
        public decimal EstimatedCost { get; set; }
        public decimal ExchangeRate { get; set; }

        public long BudgetAmountAtSubmit { get; set; }
        public long BudgetUsedAtSubmit { get; set; }
        public long BudgetRemainingAtSubmit { get; set; }

        public string RequesterSign { get; set; }
        public DateTime CreatedDate { get; set; }

        public string StatusName { get; set; }
        public string StatusColor { get; set; }

        public BudgetViewModel Budget { get; set; }
        public List<EmployeeViewModel> Employees { get; set; }
        public TravelExpenseCostViewModel CostDetails { get; set; }
        public List<string> AttachmentFiles { get; set; } = new List<string>();
        public List<ApprovalInfoViewModel> Approvals { get; set; }
    }
}