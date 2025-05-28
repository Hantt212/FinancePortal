using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FinancePortal.ViewModels
{
    public enum TravelExpenseStatusEnum
    {
        RequesterPending = 1,
        RequesterCancelled = 2,
        HODPending = 3,
        HODApproved = 4,
        HODRejected = 5,
        GLPending = 6,
        GLApproved = 7,
        FCPending = 8,
        FCApproved = 9,
        FCRejected = 10,
        Done = 11
    }
}