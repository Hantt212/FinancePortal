using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FinancePortal.ViewModels
{
    public class RequestSummariesByUserResult
    {
        public int ID { get; set; }
        public string TarNo { get; set; }
        public string TripPurpose { get; set; }
        public DateTime RequestDate { get; set; }
        public Int64 EstimatedCost { get; set; }
        public string CreatedBy { get; set; }
        public string DisplayName { get; set; }
        public string ColorCode { get; set; }
        public int EditMode { get; set; }
    }
}