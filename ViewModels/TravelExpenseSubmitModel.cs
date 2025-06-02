using FinancePortal.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FinancePortal.ViewModels
{
    public class TravelExpenseSubmitModel
    {
        public int ID { get; set; }
        public string TarNo { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int TripDays { get; set; }
        public DateTime RequestDate { get; set; }
        public int BudgetID { get; set; }
        public string TripPurpose { get; set; }
        public long EstimatedCost { get; set; }
        public long ExchangeRate { get; set; }
        public DateTime CreatedDate { get; set; }
        public string RequesterSign { get; set; }
        public int StatusID { get; set; }
        public string StatusName { get; set; }
        public string StatusColor { get; set; }
        public ApproverViewModel Approver { get; set; }
        public List<EmployeeViewModel> Employees { get; set; } = new List<EmployeeViewModel>();
        public List<string> AttachmentFiles { get; set; } = new List<string>();

        // 🔥 NEW SECTION: Cost Details
        public TravelExpenseCostViewModel CostDetails { get; set; } = new TravelExpenseCostViewModel();
    }

}