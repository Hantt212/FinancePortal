using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FinancePortal.ViewModels
{
    public class TravelExpenseCostViewModel
    {
        public long CostAir { get; set; }
        public long CostHotel { get; set; }
        public long CostMeal { get; set; }
        public long CostOther { get; set; }
    }
}