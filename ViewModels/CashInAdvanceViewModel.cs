using System;
using System.Collections.Generic;
using System.EnterpriseServices.Internal;

namespace FinancePortal.ViewModels
{
    public class CashInAdvanceViewModel
    {
        public int ID { get; set; }
        public int TravelExpenseID { get; set; }
        public string EmployeeID { get; set; }
        public string EmployeeName { get; set; }
        public string Department { get; set; }
        public string Reason { get; set; }
        public long RequiredCash { get; set; }
        public DateTime RequiredDate { get; set; }
        public DateTime ReturnedDate { get; set; }
        public string BeneficialName { get; set; }
        public string BankBranch { get; set; }
        public string AccountNo { get; set; }

        public string HODID { get; set; }
        public string HODName { get; set; }
        public string HODPosition { get; set; }
        public string HODEmail { get; set; }

        public DateTime CreatedDate { get; set; }
        public string RequesterSign { get; set; }

    }
}