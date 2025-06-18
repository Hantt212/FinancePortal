using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FinancePortal.ViewModels
{
    public class TravelInfoViewModel
    {
        public string FormName { get; set; }
        public string CreatedDate { get; set; }
        public int ID { get; set; }
        public int TravelExpenseID { get; set; }
        public string TokenID { get; set; }
        public int EditMode { get; set; }
        public int CashMode { get; set; }

        public string StatusName { get; set; }
        public string StatusColor { get; set; }
    }
}