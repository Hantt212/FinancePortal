using FinancePortal.Models;
using FinancePortal.ViewModels;
using Microsoft.AspNet.Identity;
using System;
using System.Collections.Generic;
using System.Data.Entity.Validation;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Web;

namespace FinancePortal.Dao
{
    public static class ExpenseClaimDao
    {
        const string RequesterRole = "Requester";
        const string HODRole = "HOD";
        const string APRole = "AP";
        const string FCRole = "FC";

        public static PaymentViewModel GetInitPayment(int ciaID)
        {
            using (var db = new FinancePortalEntities())
            {
                var payments = db.Payments.Select(item => new PaymentsModel { ID = item.ID, PaymentName = item.PaymentName}).ToList();
                var costs = TravelExpenseDao.GetCostBudgetList();

                var ciaInfo = db.CashInAdvances.Find(ciaID);
                int travelExpenseId = ciaInfo != null ? ciaInfo.TravelExpenseID : 0;
                var budgetApproved = (from d in db.TravelExpenseCostDetails
                                      join cb in db.TravelExpenseCostBudgets on d.CostBudgetID equals cb.ID
                                      join b in db.TravelExpenseBudgets on cb.BudgetID equals b.ID
                                      where d.TravelExpenseID == travelExpenseId && d.IsShown
                                      group d by b.BudgetName into g
                                      select new BudgetViewModel
                                      {
                                          BudgetName = g.Key,
                                          BudgetAmount = g.Sum(x => x.CostAmount) // sum budget approved
                                      }).ToList();

                var result = new PaymentViewModel();
                result.Payments = payments;
                result.Budgets = budgetApproved;
                result.Costs = costs;
                return result;
            }
        }

        public static ExpenseClaimViewModel GetExpenseClaimByCIAID(int ciaID)
        {
            using (var db = new FinancePortalEntities())
            {
                int ID = 0;
                var ciaInfo = db.CashInAdvances.Find(ciaID);

                ExpenseClaimViewModel viewModel = new ExpenseClaimViewModel();
                ExpenseClaim expenseClaim = db.ExpenseClaims.Where(c => c.CIAID == ciaID).FirstOrDefault();
                if (expenseClaim != null)
                {
                    ID = expenseClaim.ID;
                    viewModel.ID = expenseClaim.ID;
                    viewModel.ReportDate = expenseClaim.ReportDate;
                    viewModel.FromDate = expenseClaim.FromDate;
                    viewModel.ToDate = expenseClaim.ToDate;
                    viewModel.BusinessPurpose = expenseClaim.BusinessPurpose;
                    viewModel.Currency = expenseClaim.Currency;
                    viewModel.Rate = (double)expenseClaim.Rate;
                    viewModel.TotalExpense = expenseClaim.TotalExpense;
                    viewModel.CashReceived = ciaInfo == null ? ciaInfo.RequiredCash : 0;
                    viewModel.BalanceCompany = expenseClaim.BalanceCompany;
                    viewModel.BalanceEmployee = expenseClaim.BalanceEmployee;
                    viewModel.TotalCharges = expenseClaim.TotalCharges;
                    viewModel.RequesterSign = expenseClaim.RequesterSignature;


                    //Get Approval HOD Info 
                    ExpenseClaimApproval hodApprovalInfo = db.ExpenseClaimApprovals.Where(c => c.ECID == ID).FirstOrDefault();
                    if (hodApprovalInfo != null)
                    {
                        viewModel.HODID = hodApprovalInfo.ApproverID;
                        viewModel.HODName = hodApprovalInfo.ApproverName;
                        viewModel.HODEmail = hodApprovalInfo.ApproverEmail;
                        viewModel.HODPosition = hodApprovalInfo.ApproverPosition;
                    }
                }
                else
                {
                    //Get Approval HOD Info from travel expense
                    CashInAdvanceApproval hodApprovalInfo = db.CashInAdvanceApprovals.Where(t => t.CIAID == ID && t.ApprovalStep == 1).FirstOrDefault();
                    if (hodApprovalInfo != null)
                    {
                        viewModel.HODID = hodApprovalInfo.ApproverID;
                        viewModel.HODName = hodApprovalInfo.ApproverName;
                        viewModel.HODEmail = hodApprovalInfo.ApproverEmail;
                        viewModel.HODPosition = hodApprovalInfo.ApproverPosition;
                    }
                }

                viewModel.CIAID = ciaID;
                //Get requester
                User user = db.Users.Where(u => u.UserName == ciaInfo.CreatedBy).FirstOrDefault();
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

                    // Send email
                    var userRequester = db.Users.FirstOrDefault(item => item.UserName == username);
                    string mailRequester = userRequester?.UserEmailAddress ?? string.Empty;

                    var mailContent = new MailContentModel();
                    mailContent.TarNumber = db.TravelExpenses.Find(ciaForm.TravelExpenseID)?.TarNo;
                    mailContent.RecipientTo = model.HODEmail;
                    mailContent.RecipientCc = mailRequester;
                    mailContent.Content = "Please be informed that you have a expense claim waiting for approval.";
                    TravelExpenseDao.SendEmail(mailContent);
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
                   // ciaForm.StatusID = model.StatusID;
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

                    //If travel status is Reject, then update as a new travel
                    if (ciaForm.StatusID == (int)TravelExpenseStatusEnum.RejectedHOD
                        || ciaForm.StatusID == (int)TravelExpenseStatusEnum.RejectedAP
                        || ciaForm.StatusID == (int)TravelExpenseStatusEnum.RejectedFC)
                    {
                        ciaForm.StatusID = (int)TravelExpenseStatusEnum.WaitingHOD;
                    }

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

        public static CashInAdvanceViewModel GetCIAViewDetails(int id)
        {
            using (var db = new FinancePortalEntities())
            {
                var c = db.CashInAdvances.FirstOrDefault(x => x.ID == id);
                if (c == null) return null;

                var s = db.TravelExpenseStatus.FirstOrDefault(x => x.ID == c.StatusID);

                var approvals = db.CashInAdvanceApprovals
                                  .Where(a => a.CIAID == c.ID)
                                  .OrderBy(a => a.ApprovalStep)
                                  .Select(a => new ApprovalInfoViewModel
                                  {
                                      ApprovalStep = (int)a.ApprovalStep,
                                      ApproverID = a.ApproverID,
                                      ApproverName = a.ApproverName,
                                      ApproverEmail = a.ApproverEmail,
                                      ApproverPosition = a.ApproverPosition,
                                      ApproverSignature = a.ApproverSignature,
                                      ApproverSignDate = a.ApproverSignDate,
                                      IsApproved = a.IsApproved
                                  }).ToList();

                // Check if FC approval step exists, if not, preload from User table
                bool hasFCApproval = approvals.Any(a => a.ApprovalStep == (int)ApprovalStep.FC);
                if (!hasFCApproval)
                {
                    var fcUser = (from u in db.Users
                                  join ur in db.UserRoles on u.UserId equals ur.UserId
                                  join r in db.Roles on ur.RoleId equals r.RoleId
                                  where r.RoleName == FCRole && u.IsShown && u.IsActive && ur.IsShown == true
                                  select new ApprovalInfoViewModel
                                  {
                                      ApprovalStep = (int)ApprovalStep.FC,
                                      ApproverID = u.EmployeeCode,
                                      ApproverName = u.UserName,
                                      ApproverEmail = u.UserEmailAddress,
                                      ApproverPosition = "Finance Controller", // or u.Position if available
                                      IsApproved = false
                                  }).FirstOrDefault();

                    if (fcUser != null)
                        approvals.Add(fcUser);
                }

                var model = new CashInAdvanceViewModel
                {
                    ID = c.ID,
                    TravelExpenseID = c.TravelExpenseID,
                    Reason = c.Reason,
                    RequiredCash = c.RequiredCash,
                    RequiredDate = c.RequiredDate,
                    ReturnedDate = c.ReturnedDate,
                    BeneficialName = c.BeneficialName,
                    BankBranch = c.BankBranch,
                    AccountNo = c.AccountNo,

                    RequesterSign = c.RequesterSignature,
                    CreatedDate = c.CreatedDate,

                    StatusID = c.StatusID,
                    StatusName = s?.DisplayName,
                    StatusColor = s?.ColorCode,

                    Approvals = approvals.OrderBy(a => a.ApprovalStep).ToList(),
                    //AttachmentFiles = db.TravelExpenseAttachmentFiles
                    //                .Where(a => a.TravelExpenseID == c.ID && a.IsShown == true)
                    //                .Select(a => a.FileName).ToList(),
                };

                return model;
            }
        }

        public static OperationResult HandleCIAByRequester(int ciaId, string approverUsername)
        {
            using (var db = new FinancePortalEntities())
            {
                var now = DateTime.Now;

                var ciaInfo = db.CashInAdvances.FirstOrDefault(c => c.ID == ciaId && c.IsShown);
                if (ciaInfo == null)
                    return new OperationResult { Success = false, Message = "Request not found." };

                ciaInfo.StatusID = (int)TravelExpenseStatusEnum.Cancelled;
                ciaInfo.UpdatedBy = approverUsername;
                ciaInfo.UpdatedDate = now;

                db.SaveChanges();

                return new OperationResult
                {
                    Success = true,
                    Message = "Cash In Advance cancel successfully."
                };
            }
        }

        public static OperationResult HandleCIAByHOD(int ciaId, string approverUsername, bool isApprove)
        {
            using (var db = new FinancePortalEntities())
            {
                var now = DateTime.Now;

                var approval = db.CashInAdvanceApprovals
                    .FirstOrDefault(a => a.CIAID == ciaId && a.ApprovalStep == (int)ApprovalStep.HOD);

                if (approval == null)
                    return new OperationResult { Success = false, Message = "HOD approval record not found." };
                var apUser = db.Users
                            .Where(u => u.UserRoles.Any(r => r.Role.RoleName == APRole && r.IsShown == true && u.IsActive && u.IsShown))
                            .FirstOrDefault();

                if (isApprove)
                {
                    if (apUser == null)
                        return new OperationResult { Success = false, Message = "No AP is assigned. Cannot proceed with approval." };
                }

                approval.ApproverSignature = approverUsername;
                approval.ApproverSignDate = now;
                approval.IsApproved = isApprove;
                approval.UpdatedDate = now;
                approval.UpdatedBy = approverUsername;

                var ciaInfo = db.CashInAdvances.FirstOrDefault(c => c.ID == ciaId && c.IsShown);
                if (ciaInfo == null)
                    return new OperationResult { Success = false, Message = "Cash In Advance not found." };

                ciaInfo.StatusID = isApprove
                    ? (int)TravelExpenseStatusEnum.WaitingAP
                    : (int)TravelExpenseStatusEnum.RejectedHOD;
                ciaInfo.UpdatedBy = approverUsername;
                ciaInfo.UpdatedDate = now;

                var userRequester = db.Users.FirstOrDefault(item => item.UserName == ciaInfo.CreatedBy);
                string mailRequester = userRequester?.UserEmailAddress ?? string.Empty;
                var mailContent = new MailContentModel();
                mailContent.TarNumber = db.TravelExpenses.Find(ciaInfo.TravelExpenseID).TarNo;

                if (isApprove)
                {
                    var apApprovalExists = db.CashInAdvanceApprovals
                        .Any(a => a.CIAID == ciaId && a.ApprovalStep == (int)ApprovalStep.AP);

                    if (!apApprovalExists)
                    {
                        if (apUser != null)
                        {
                            db.CashInAdvanceApprovals.Add(new CashInAdvanceApproval
                            {
                                CIAID = ciaId,
                                ApprovalStep = (int)ApprovalStep.AP,
                                ApproverID = apUser.EmployeeCode,
                                ApproverName = apUser.UserName,
                                ApproverEmail = apUser.UserEmailAddress,
                                ApproverPosition = "AP Team",
                                IsApproved = false,
                                CreatedDate = now,
                                CreatedBy = approverUsername
                            });

                            //Approve:Send mail to GL
                            mailContent.RecipientTo = apUser.UserEmailAddress;
                            mailContent.RecipientCc = mailRequester;
                            mailContent.Content = "Please be informed that you have a cash in advance waiting for approval.";
                            TravelExpenseDao.SendEmail(mailContent);
                        }
                    }
                }

                //Reject:Send mail to Requester
                mailContent.RecipientTo = mailRequester;
                mailContent.Content = "Please be informed that your cash in advance has been <strong>denied</strong> by HOD.";
                TravelExpenseDao.SendEmail(mailContent);

                db.SaveChanges();
                return new OperationResult
                {
                    Success = true,
                    Message = isApprove ? "Cash in advance approved successfully." : "Cash in advance rejected successfully."
                };
            }
        }

        public static OperationResult HandleCIAByAP(int ciaId, string approverUsername, bool isApprove)
        {
            using (var db = new FinancePortalEntities())
            {
                var now = DateTime.Now;

                var ciaInfo = db.CashInAdvances.FirstOrDefault(c => c.ID == ciaId && c.IsShown);
                if (ciaInfo == null)
                    return new OperationResult { Success = false, Message = "Request not found." };

                var approval = db.CashInAdvanceApprovals
                    .FirstOrDefault(a => a.CIAID == ciaId && a.ApprovalStep == (int)ApprovalStep.AP);
                if (approval == null)
                    return new OperationResult { Success = false, Message = "AP Approval record not found." };
                var fcUser = db.Users
                            .Where(u => u.UserRoles.Any(r => r.Role.RoleName == FCRole && r.IsShown == true && u.IsActive && u.IsShown))
                            .FirstOrDefault();

                // Update approval
                approval.ApproverSignature = approverUsername;
                approval.ApproverSignDate = now;
                approval.IsApproved = isApprove;
                approval.UpdatedDate = now;
                approval.UpdatedBy = approverUsername;

                ciaInfo.StatusID = isApprove ? (int)TravelExpenseStatusEnum.WaitingFC : (int)TravelExpenseStatusEnum.RejectedAP;
                ciaInfo.UpdatedBy = approverUsername;
                ciaInfo.UpdatedDate = now;

                //Approve:Send mail to FC
                var userRequester = db.Users.FirstOrDefault(item => item.UserName == ciaInfo.CreatedBy);
                string mailRequester = userRequester?.UserEmailAddress ?? string.Empty;

                var mailContent = new MailContentModel();
                mailContent.TarNumber = db.TravelExpenses.Find(ciaInfo.TravelExpenseID).TarNo;

                if (isApprove)
                {
                    if (fcUser == null)
                    {
                        return new OperationResult
                        {
                            Success = false,
                            Message = "No Finance Controller is assigned.Cannot proceed with approval."
                        };
                    }
                    var fcApprovalExists = db.CashInAdvanceApprovals
                        .Any(a => a.CIAID == ciaId && a.ApprovalStep == (int)ApprovalStep.FC);



                    if (!fcApprovalExists)
                    {
                        if (fcUser != null)
                        {
                            db.CashInAdvanceApprovals.Add(new CashInAdvanceApproval
                            {
                                CIAID = ciaId,
                                ApprovalStep = (int)ApprovalStep.FC,
                                ApproverID = fcUser.EmployeeCode,
                                ApproverName = fcUser.UserName,
                                ApproverEmail = fcUser.UserEmailAddress,
                                ApproverPosition = "Finance Controller",
                                IsApproved = false,
                                CreatedDate = now,
                                CreatedBy = approverUsername
                            });
                        }
                    }


                    mailContent.RecipientTo = fcUser.UserEmailAddress;
                    mailContent.RecipientCc = mailRequester;
                    mailContent.Content = "Please be informed that you have a cash in advance waiting for approval.";
                    TravelExpenseDao.SendEmail(mailContent);
                }

                mailContent.RecipientTo = mailRequester;
                mailContent.Content = "Please be informed that your cash in advance has been <strong>denied</strong> by AP.";
                TravelExpenseDao.SendEmail(mailContent);

                db.SaveChanges();

                return new OperationResult
                {
                    Success = true,
                    Message = isApprove ? "Request approved successfully." : "Request rejected successfully."
                };
            }
        }

        public static OperationResult HandleCIAByFC(int ciaId, string approverUsername, bool isApprove)
        {
            using (var db = new FinancePortalEntities())
            {
                var now = DateTime.Now;

                var ciaInfo = db.CashInAdvances.FirstOrDefault(t => t.ID == ciaId && t.IsShown);
                if (ciaInfo == null)
                    return new OperationResult { Success = false, Message = "Cash in advance not found." };

                var approval = db.CashInAdvanceApprovals
                    .FirstOrDefault(a => a.CIAID == ciaId && a.ApprovalStep == (int)ApprovalStep.FC);
                if (approval == null)
                    return new OperationResult { Success = false, Message = "FC Approval record not found." };

                // Update approval
                approval.ApproverSignature = approverUsername;
                approval.ApproverSignDate = now;
                approval.IsApproved = isApprove;
                approval.UpdatedDate = now;
                approval.UpdatedBy = approverUsername;

                ciaInfo.StatusID = isApprove
                    ? (int)TravelExpenseStatusEnum.CIAApproved
                    : (int)TravelExpenseStatusEnum.RejectedFC;
                ciaInfo.UpdatedBy = approverUsername;
                ciaInfo.UpdatedDate = now;

                db.SaveChanges();

                //Approve:Send mail to Requester
                var userRequester = db.Users.FirstOrDefault(item => item.UserName == ciaInfo.CreatedBy);
                string mailRequester = userRequester?.UserEmailAddress ?? string.Empty;

                var mailContent = new MailContentModel();
                mailContent.TarNumber = db.TravelExpenses.Find(ciaInfo.ID)?.TarNo;
                mailContent.RecipientTo = mailRequester;
                if (isApprove)
                {
                    mailContent.Content = "Please be informed that your cash in advance has been done.";
                }
                else
                {
                    mailContent.Content = "Please be informed that your cash in advance has been <strong>denied</strong> by FC.";
                }
                TravelExpenseDao.SendEmail(mailContent);

                return new OperationResult
                {
                    Success = true,
                    Message = isApprove ? "Request approved successfully." : "Request rejected successfully."
                };
            }
        }

    }
}