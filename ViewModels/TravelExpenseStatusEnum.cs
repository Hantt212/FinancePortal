using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FinancePortal.ViewModels
{
    public enum TravelExpenseStatusEnum
    {
        RequesterPending = 1,
        HODPending = 2,
        HODApproved = 3,
        HODRejected = 4,
        FCPending = 5,
        FCApproved = 6,
        FCRejected = 7,
        Done = 8
    }
}