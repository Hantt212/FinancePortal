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
        WaitingFC = 5,
        RejectedFC = 6,
        TARApproved = 7
    }

    public enum ApprovalStep
    {
        HOD = 1,
        GL = 2,
        FC = 3
    }

    public enum TypeAttachmentFile
    {
        TravelExpense = 1,
        CashAdvantage = 2,
    }
}