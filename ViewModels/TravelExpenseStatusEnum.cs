using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FinancePortal.ViewModels
{
    public enum TravelExpenseStatusEnum
    {
        Cancelled = 1,
        WaitingHOD = 2,
        RejectedHOD = 3,
        WaitingGL = 4,
        WaitingAP = 5,
        RejectedAP = 6,
        WaitingFC = 7,
        RejectedFC = 8,
        TARApproved = 9,
        CIAApproved = 10,
        CFApproved = 11,
    }

    public enum ApprovalStep
    {
        HOD = 1,
        GL = 2,
        AP = 2,
        FC = 3
    }

    public enum TypeAttachmentFile
    {
        TravelExpense = 1,
        CashAdvantage = 2,
    }
}