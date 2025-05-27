using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FinancePortal.ViewModels
{
    public class ApprovalInfoViewModel
    {
        public int ApprovalStep { get; set; }           // 1 = HOD, 2 = FC
        public string ApproverID { get; set; }           // Employee Code
        public string ApproverName { get; set; }
        public string ApproverEmail { get; set; }
        public string ApproverPosition { get; set; }
        public string ApproverSignature { get; set; }
        public DateTime? ApproverSignDate { get; set; }
        public bool? IsApproved { get; set; }            // true = approved, false = rejected, null = pending
    }

}