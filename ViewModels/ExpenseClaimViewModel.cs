using System;
using System.Collections.Generic;
using System.EnterpriseServices.Internal;

namespace FinancePortal.ViewModels
{
    public class ExpenseClaimViewModel
    {
        public int ID { get; set; }
        public int CIAID { get; set; }
        public string EmployeeID { get; set; }
        public string EmployeeName { get; set; }
        public string Department { get; set; }
        public string BusinessPurpose { get; set; }
        public DateTime ReportDate { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string Currency { get; set; }
        public double Rate { get; set; }
        public long TotalExpense { get; set; }
        public long CashReceived { get; set; }
        public long BalanceCompany { get; set; }
        public long BalanceEmployee { get; set; }
        public long TotalCharges { get; set; }

        public string HODID { get; set; }
        public string HODName { get; set; }
        public string HODPosition { get; set; }
        public string HODEmail { get; set; }

        public DateTime CreatedDate { get; set; }
        public string RequesterSign { get; set; }

        public int StatusID { get; set; }
        public string StatusName { get; set; } = string.Empty;  
        public string StatusColor { get; set; }

        public List<ApprovalInfoViewModel> Approvals { get; set; }
        public List<BudgetViewModel>BudgetApproved { get; set; }
    }
}