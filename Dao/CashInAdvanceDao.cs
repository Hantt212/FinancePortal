using FinancePortal.Models;
using FinancePortal.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.Entity.Validation;
using System.Linq;
using System.Web;

namespace FinancePortal.Dao
{
    public static class CashInAdvanceDao
    {
        public static CashInAdvanceViewModel GetCashInAdvanceByTravelID(int travelID)
        {
            using (var db = new FinancePortalEntities())
            {
                CashInAdvanceViewModel viewModel = new CashInAdvanceViewModel();
                int CIAID = 0;
                CashInAdvance cashInAdvance = db.CashInAdvances.Where(c => c.TravelExpenseID == travelID).FirstOrDefault();
                if (cashInAdvance != null)
                {
                    CIAID = cashInAdvance.ID;
                    viewModel.ID = cashInAdvance.ID;
                    viewModel.Reason = cashInAdvance.Reason;
                    viewModel.RequiredCash = cashInAdvance.RequiredCash;
                    viewModel.RequiredDate = cashInAdvance.RequiredDate;
                    viewModel.ReturnedDate = cashInAdvance.ReturnedDate;
                    viewModel.RequesterSign = cashInAdvance.RequesterSignature;


                    //Get Approval HOD Info 
                    CashInAdvanceApproval hodApprovalInfo = db.CashInAdvanceApprovals.Where(c => c.CIAID == CIAID).FirstOrDefault();
                    if (hodApprovalInfo != null)
                    {
                        viewModel.HODID = hodApprovalInfo.ApproverID;
                        viewModel.HODName = hodApprovalInfo.ApproverName;
                        viewModel.HODEmail = hodApprovalInfo.ApproverEmail;
                        viewModel.HODPosition = hodApprovalInfo.ApproverPosition;
                    }

                    //Get Bank Info
                    viewModel.BeneficialName = cashInAdvance.BeneficialName;
                    viewModel.BankBranch = cashInAdvance.BankBranch;
                    viewModel.AccountNo = cashInAdvance.AccountNo;
                }
                else
                {
                    //Get Approval HOD Info from travel expense
                    TravelExpenseApproval hodApprovalInfo = db.TravelExpenseApprovals.Where(t => t.TravelExpenseID == travelID && t.ApprovalStep == 1).FirstOrDefault();
                    if (hodApprovalInfo != null)
                    {
                        viewModel.HODID = hodApprovalInfo.ApproverID;
                        viewModel.HODName = hodApprovalInfo.ApproverName;
                        viewModel.HODEmail = hodApprovalInfo.ApproverEmail;
                        viewModel.HODPosition = hodApprovalInfo.ApproverPosition;
                    }
                }

                TravelExpense travelInfo = db.TravelExpenses.Find(travelID);
                viewModel.TravelExpenseID = travelID;
                //Get requester
                User user = db.Users.Where(u => u.UserName == travelInfo.CreatedBy).FirstOrDefault();
                viewModel.EmployeeID = user.EmployeeCode;
                viewModel.EmployeeName = user.UserName;
                viewModel.Department = user.Department;

                


                return viewModel;
            }
        }

        public static bool SaveCashInAdvance(CashInAdvanceViewModel model)
        {
            using (var db = new FinancePortalEntities())
            {
                // Session data
                var username = HttpContext.Current.Session["Username"]?.ToString();
                var employeeID = HttpContext.Current.Session["EmployeeID"]?.ToString();
                var email = HttpContext.Current.Session["Email"]?.ToString();
                var position = HttpContext.Current.Session["Position"]?.ToString();
                try
                {
                    // Insert CashInAdvance
                    CashInAdvance ciaForm = new CashInAdvance();
                    ciaForm.TravelExpenseID = model.TravelExpenseID;
                    ciaForm.Reason = model.Reason;
                    ciaForm.RequiredCash = model.RequiredCash;
                    ciaForm.RequiredDate = model.RequiredDate;
                    ciaForm.ReturnedDate = model.ReturnedDate;
                    ciaForm.BeneficialName = model.BeneficialName;
                    ciaForm.BankBranch = model.BankBranch;
                    ciaForm.AccountNo = model.AccountNo;
                    ciaForm.RequesterSignature = model.RequesterSign;
                    ciaForm.IsShown = true;
                    ciaForm.CreatedBy = username;
                    ciaForm.CreatedDate = DateTime.Now;
                    ciaForm.StatusID = (int)TravelExpenseStatusEnum.WaitingHOD;
                    db.CashInAdvances.Add(ciaForm);
                    db.SaveChanges();

                    // Insert CashInAdvanceApproval
                    CashInAdvanceApproval approval = new CashInAdvanceApproval();
                    approval.CIAID = ciaForm.ID;
                    approval.ApprovalStep = 1;
                    approval.ApproverID = model.HODID;
                    approval.ApproverName = model.HODName;
                    approval.ApproverEmail = model.HODEmail;
                    approval.ApproverPosition = model.HODPosition;
                    approval.ApproverSignature = model.HODName;
                    approval.ApproverSignDate = DateTime.Now;
                    approval.IsApproved = false;
                    approval.CreatedBy = username;
                    approval.CreatedDate = DateTime.Now;
                    db.CashInAdvanceApprovals.Add(approval);
                    db.SaveChanges();
                    return true;
                }
                catch (DbEntityValidationException ex)
                {
                    foreach (var entityError in ex.EntityValidationErrors)
                    {
                        foreach (var validationError in entityError.ValidationErrors)
                        {
                            System.Diagnostics.Debug.WriteLine($"Property: {validationError.PropertyName}, Error: {validationError.ErrorMessage}");
                        }
                    }

                    throw; // Preserve stack trace
                }
            }
        }


        public static bool UpdateCashInAdvance(CashInAdvanceViewModel model)
        {
            using (var db = new FinancePortalEntities())
            {
                // Session data
                var username = HttpContext.Current.Session["Username"]?.ToString();
                var employeeID = HttpContext.Current.Session["EmployeeID"]?.ToString();
                var email = HttpContext.Current.Session["Email"]?.ToString();
                var position = HttpContext.Current.Session["Position"]?.ToString();
                try
                {
                    // Insert CashInAdvance
                    CashInAdvance ciaForm = db.CashInAdvances.Find(model.ID);
                    ciaForm.Reason = model.Reason;
                    ciaForm.RequiredCash = model.RequiredCash;
                    ciaForm.RequiredDate = model.RequiredDate;
                    ciaForm.ReturnedDate = model.ReturnedDate;
                    ciaForm.BeneficialName = model.BeneficialName;
                    ciaForm.BankBranch = model.BankBranch;
                    ciaForm.AccountNo = model.AccountNo;
                    ciaForm.RequesterSignature = model.RequesterSign;
                    ciaForm.UpdatedBy = username;
                    ciaForm.UpdatedDate = DateTime.Now;

                    // Insert CashInAdvanceApproval
                    CashInAdvanceApproval approval = db.CashInAdvanceApprovals.Where(c => c.CIAID == ciaForm.ID && c.ApprovalStep == 1).FirstOrDefault();
                    approval.ApproverID = model.HODID;
                    approval.ApproverName = model.HODName;
                    approval.ApproverEmail = model.HODEmail;
                    approval.ApproverPosition = model.HODPosition;
                    approval.ApproverSignature = model.HODName;
                    approval.ApproverSignDate = DateTime.Now;
                    approval.UpdatedBy = username;
                    approval.UpdatedDate = DateTime.Now;
                    db.SaveChanges();
                    return true;
                }
                catch (DbEntityValidationException ex)
                {
                    foreach (var entityError in ex.EntityValidationErrors)
                    {
                        foreach (var validationError in entityError.ValidationErrors)
                        {
                            System.Diagnostics.Debug.WriteLine($"Property: {validationError.PropertyName}, Error: {validationError.ErrorMessage}");
                        }
                    }

                    throw; // Preserve stack trace
                }
            }
        }
    }
}