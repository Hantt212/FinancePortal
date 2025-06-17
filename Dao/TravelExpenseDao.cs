using FinancePortal.Models;
using FinancePortal.ViewModels;
using Microsoft.AspNet.Identity;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Data.Entity.Validation;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Net;
using System.Web;
using System.Web.Services.Description;
using System.Runtime.Remoting.Lifetime;
using System.Security.Policy;
using Antlr.Runtime;

namespace FinancePortal.Dao
{
    public static class TravelExpenseDao
    {
        #region Expense Form
        const string RequesterRole = "Requester";
        const string HODRole = "HOD";
        const string GLRole = "GL";
        const string FCRole = "FC";

        public static EmployeeViewModel GetEmployeeByCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return null;

            using (var context = new AnnualEmployeeEntities())
            {
                var result = context.Database.SqlQuery<EmployeeViewModel>(
                   "EXEC spSelectEmployeeInfo @EmployeeID",
                   new SqlParameter("@EmployeeID", code)).FirstOrDefault();
                if (result == null) return null;

                if (result.ImagePath != null)
                {
                    result.EmployeeImage = String.Format("data:image/jpg;base64,{0}", Convert.ToBase64String(result.ImagePath));
                }
                else
                {
                    result.EmployeeImage = "/Assets/images/user.png";
                }
                return result;
            }
            
        }

        public static bool SaveTravelExpense(TravelExpenseSubmitModel model, List<string> newAttachFiles)
        {
            using (var db = new FinancePortalEntities())
            {
                // Session data
                var username = HttpContext.Current.Session["Username"]?.ToString();
                var employeeID = HttpContext.Current.Session["EmployeeID"]?.ToString();
                var email = HttpContext.Current.Session["Email"]?.ToString();
                var position = HttpContext.Current.Session["Position"]?.ToString();

                // 1. Validate cost detail existence
                if (model.CostDetails == null || !model.CostDetails.Any())
                    throw new Exception("Selected budget not found or inactive.");

                // 2. Create main TravelExpense record
                var travel = new TravelExpense
                {
                    FromDate = model.FromDate,
                    ToDate = model.ToDate,
                    TripDays = model.TripDays,
                    RequestDate = model.RequestDate,
                    TripPurpose = model.TripPurpose,
                    EstimatedCost = model.EstimatedCost,
                    ExchangeRate = model.ExchangeRate,
                    RequesterSignature = model.RequesterSign,
                    CreatedBy = username,
                    CreatedDate = DateTime.Now,
                    TarNo = model.TarNo,
                    IsShown = true,
                    StatusID = (int)TravelExpenseStatusEnum.WaitingHOD
                };

                db.TravelExpenses.Add(travel);
                db.SaveChanges(); // Save to generate travel.ID

                // 3. Add HOD approval
                if (model.Approver == null)
                    throw new Exception("Approver (HOD) information is required.");

                db.TravelExpenseApprovals.Add(new TravelExpenseApproval
                {
                    TravelExpenseID = travel.ID,
                    ApprovalStep = (int)ApprovalStep.HOD,
                    ApproverID = model.Approver.Code,
                    ApproverName = model.Approver.Name,
                    ApproverEmail = model.Approver.Email,
                    ApproverPosition = model.Approver.Position,
                    IsApproved = false,
                    CreatedBy = username,
                    CreatedDate = DateTime.Now
                });

                // 4. Add involved employees
                foreach (var emp in model.Employees)
                {
                    db.TravelExpenseEmployees.Add(new TravelExpenseEmployee
                    {
                        TravelExpenseID = travel.ID,
                        EmployeeCode = emp.Code,
                        FullName = emp.Name,
                        Position = emp.Position,
                        Division = emp.Division,
                        Department = emp.Department,
                        IsShown = true
                    });
                }

                // 5. Add cost details
                foreach (var costDetail in model.CostDetails)
                {
                    // Update budget 
                    //var budgetInfo = GetBudgetInfoByCostBudget(costDetail.CostBudgetID);
                    var budget = db.TravelExpenseBudgets.Find(costDetail.BudgetID);

                    if (budget != null)
                    {
                        budget.BudgetRemaining -= costDetail.CostAmount;
                        budget.BudgetUsed += costDetail.CostAmount;
                        budget.UpdatedBy = username;
                        budget.UpdatedDate = DateTime.Now;
                    }

                    // Add cost detail
                    db.TravelExpenseCostDetails.Add(new TravelExpenseCostDetail
                    {
                        TravelExpenseID = travel.ID,
                        CostBudgetID = costDetail.CostBudgetID,
                        CostAmount = costDetail.CostAmount,
                        BudgetAmountAtSubmit = budget.BudgetAmount,
                        BudgetRemainingAtSubmit = budget.BudgetRemaining,
                        BudgetUsedAtSubmit = budget.BudgetUsed,
                        IsShown = true
                    });
                    db.SaveChanges();
                }

                // 7. Add attachment files
                foreach (var fileName in newAttachFiles)
                {
                    db.TravelExpenseAttachmentFiles.Add(new TravelExpenseAttachmentFile
                    {
                        TravelExpenseID = travel.ID,
                        FileName = fileName,
                        Type = (int)TypeAttachmentFile.TravelExpense,
                        IsShown = true,
                        CreatedBy = username,
                        CreatedDate = DateTime.Now
                    });
                }
                //8. Send email
                var userRequester = db.Users.FirstOrDefault(item => item.UserName == username);
                string mailRequester = userRequester?.UserEmailAddress ?? string.Empty;

                var mailContent = new MailContentModel();
                mailContent.TarNumber = travel.TarNo;
                mailContent.RecipientTo = model.Approver.Email;
                mailContent.RecipientCc = mailRequester;
                mailContent.Content = "Please be informed that you have a travel expense request waiting for approval.";
                SendEmail(mailContent);

                // 8. Final save with validation catch
                try
                {
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


        public static bool UpdateTravelExpense(TravelExpenseSubmitModel model, List<string> newAttachFiles)
        {
            using (var db = new FinancePortalEntities())
            {
                var username = HttpContext.Current.Session["Username"]?.ToString();

                // 1. Validate TravelExpense exists
                var travel = db.TravelExpenses.FirstOrDefault(t => t.ID == model.ID && t.IsShown);
                if (travel == null)
                    throw new Exception("Travel Expense request not found.");


                // 2. Validate Budget
                var newCostDetails = model.CostDetails;
                if (newCostDetails.Count() == 0)
                    throw new Exception("Selected budget not found or inactive.");

                // 2. Save old estimated cost for budget adjustment
                long oldEstimatedCost = travel.EstimatedCost;

                // 4. Update Cost Details
                // Existing records from DB
                var costDetails = db.TravelExpenseCostDetails
                    .Where(c => c.TravelExpenseID == travel.ID && c.IsShown == true)
                    .ToList();

                // Update or Add
                foreach (var inputItem in newCostDetails)
                {
                    var currentCostDetail = costDetails.FirstOrDefault(c => c.CostBudgetID == inputItem.CostBudgetID);

                    //var budgetInfo = GetBudgetInfoByCostBudget(inputItem.CostBudgetID);
                    var budget = db.TravelExpenseBudgets.Find(inputItem.BudgetID);

                    if (currentCostDetail != null)
                    {
                        // Restore budget
                        budget.BudgetRemaining = budget.BudgetRemaining + currentCostDetail.CostAmount - inputItem.CostAmount;
                        budget.BudgetUsed = budget.BudgetUsed - currentCostDetail.CostAmount + inputItem.CostAmount;

                        // Update cost detail
                        currentCostDetail.CostAmount = inputItem.CostAmount;
                        currentCostDetail.BudgetAmountAtSubmit = budget.BudgetAmount;
                        currentCostDetail.BudgetRemainingAtSubmit = budget.BudgetRemaining;
                        currentCostDetail.BudgetUsedAtSubmit = budget.BudgetUsed;
                    }
                    else
                    {
                        // Update budget
                        budget.BudgetRemaining -= inputItem.CostAmount;
                        budget.BudgetUsed += inputItem.CostAmount;

                        // Add new cost detail
                        var newCostDetail = new TravelExpenseCostDetail
                        {
                            TravelExpenseID = travel.ID,
                            CostBudgetID = inputItem.CostBudgetID,
                            CostAmount = inputItem.CostAmount,
                            BudgetAmountAtSubmit = budget.BudgetAmount,
                            BudgetRemainingAtSubmit = budget.BudgetRemaining,
                            BudgetUsedAtSubmit = budget.BudgetUsed,
                            IsShown = true
                        };
                        db.TravelExpenseCostDetails.Add(newCostDetail);
                    }
                    budget.UpdatedBy = username;
                    budget.UpdatedDate = DateTime.Now;
                    db.SaveChanges();
                }

                // Remove items not in the new list
                var inputBudgetIds = newCostDetails.Select(i => i.CostBudgetID).ToList();
                var itemsToRemove = costDetails
                    .Where(c => !inputBudgetIds.Contains(c.CostBudgetID))
                    .ToList();

                foreach (var item in itemsToRemove)
                {
                    TravelExpenseCostDetail detail = db.TravelExpenseCostDetails.Find(item.ID);

                    // Update Budget
                    var budgetInfo = GetBudgetInfoByCostBudget(detail.CostBudgetID);
                    var budget = db.TravelExpenseBudgets.Find(budgetInfo.BudgetID);
                    budget.BudgetRemaining += detail.CostAmount;
                    budget.BudgetUsed -= detail.CostAmount;
                    budget.UpdatedBy = username;
                    budget.UpdatedDate = DateTime.Now;

                    // Update Cost detail
                    detail.IsShown = false;
                    detail.BudgetAmountAtSubmit = budget.BudgetAmount;
                    detail.BudgetRemainingAtSubmit = budget.BudgetRemaining;
                    detail.BudgetUsedAtSubmit = budget.BudgetUsed;

                    db.SaveChanges();
                }
                db.SaveChanges();



                // 5. Recalculate Estimated Cost
                int totalCostUSD = db.TravelExpenseCostDetails
                    .Where(c => c.TravelExpenseID == travel.ID && c.IsShown == true)
                    .Sum(c => c.CostAmount);
                long newEstimatedCostVND = (long)(totalCostUSD * (model.ExchangeRate > 0 ? model.ExchangeRate : 1));

                // 6. Update TravelExpense main fields
                travel.FromDate = model.FromDate;
                travel.ToDate = model.ToDate;
                travel.TripDays = model.TripDays;
                travel.RequestDate = model.RequestDate;
                travel.TripPurpose = model.TripPurpose;
                travel.ExchangeRate = model.ExchangeRate;
                travel.EstimatedCost = newEstimatedCostVND;
                travel.RequesterSignature = model.RequesterSign;
                travel.UpdatedBy = username;
                travel.UpdatedDate = DateTime.Now;

                // 7. Update Employees
                var existingEmployees = db.TravelExpenseEmployees
                                          .Where(e => e.TravelExpenseID == travel.ID)
                                          .ToList();

                foreach (var oldEmp in existingEmployees)
                {
                    var updatedEmp = model.Employees.FirstOrDefault(x => x.Code == oldEmp.EmployeeCode);
                    if (updatedEmp != null)
                    {
                        oldEmp.FullName = updatedEmp.Name;
                        oldEmp.Position = updatedEmp.Position;
                        oldEmp.Division = updatedEmp.Division;
                        oldEmp.Department = updatedEmp.Department;
                        oldEmp.IsShown = true;
                    }
                    else
                    {
                        oldEmp.IsShown = false; // hide employee if no longer in list
                    }
                }

                // Add new employees if any
                var existingCodes = existingEmployees.Select(e => e.EmployeeCode).ToList();
                var newEmployees = model.Employees.Where(e => !existingCodes.Contains(e.Code)).ToList();
                foreach (var emp in newEmployees)
                {
                    db.TravelExpenseEmployees.Add(new TravelExpenseEmployee
                    {
                        TravelExpenseID = travel.ID,
                        EmployeeCode = emp.Code,
                        FullName = emp.Name,
                        Position = emp.Position,
                        Division = emp.Division,
                        Department = emp.Department,
                        IsShown = true
                    });
                }

                // 8. Update Requester Approval Info (ApprovalStep 1)
                var requesterApproval = db.TravelExpenseApprovals
                    .FirstOrDefault(a => a.TravelExpenseID == travel.ID && a.ApprovalStep == (int)ApprovalStep.HOD && a.IsApproved == false);

                if (requesterApproval != null)
                {
                    requesterApproval.ApproverID = model.Approver.Code;
                    requesterApproval.ApproverEmail = model.Approver.Email;
                    requesterApproval.ApproverName = model.Approver.Name;
                    requesterApproval.ApproverPosition = model.Approver.Position;
                    requesterApproval.UpdatedBy = username;
                    requesterApproval.UpdatedDate = DateTime.Now;
                }



                //If travel status is Reject, then update as a new travel
                if (travel.StatusID == (int)TravelExpenseStatusEnum.RejectedHOD
                    || travel.StatusID == (int)TravelExpenseStatusEnum.RejectedFC)
                {
                    travel.StatusID = (int)TravelExpenseStatusEnum.WaitingHOD;
                }

                // 10. Update Attach Files
                List<TravelExpenseAttachmentFile> travelFileList = db.TravelExpenseAttachmentFiles.Where(a => a.TravelExpenseID == travel.ID).ToList();
                List<string> currFile = model.AttachmentFiles.Except(newAttachFiles).ToList();
                foreach (var travelFile in travelFileList)
                {
                    TravelExpenseAttachmentFile travelExpenseAttachmentFile = db.TravelExpenseAttachmentFiles.Find(travelFile.ID);
                    if (currFile.Contains(travelFile.FileName))
                    {
                        travelExpenseAttachmentFile.IsShown = true;
                    }
                    else
                    {
                        travelExpenseAttachmentFile.IsShown = false;
                    }
                    travelExpenseAttachmentFile.UpdatedBy = username;
                    travelExpenseAttachmentFile.UpdatedDate = DateTime.Now;
                }


                //  Add Attach Files
                foreach (var fileName in newAttachFiles)
                {
                    db.TravelExpenseAttachmentFiles.Add(new TravelExpenseAttachmentFile
                    {
                        TravelExpenseID = travel.ID,
                        FileName = fileName,
                        Type = (int)TypeAttachmentFile.TravelExpense,
                        IsShown = true,
                        CreatedBy = username,
                        CreatedDate = DateTime.Now
                    });
                }

                // 11. Save Changes
                try
                {
                    db.SaveChanges();
                }
                catch (DbEntityValidationException ex)
                {
                    foreach (var validationErrors in ex.EntityValidationErrors)
                    {
                        foreach (var validationError in validationErrors.ValidationErrors)
                        {
                            System.Diagnostics.Debug.WriteLine($"Property: {validationError.PropertyName} Error: {validationError.ErrorMessage}");
                        }
                    }
                    throw; // rethrow to catch full error
                }

                return true;
            }
        }

        public static string GenerateTARNumber()
        {
            using (var db = new FinancePortalEntities())
            {
                int currentYear = DateTime.Now.Year;

                // Count only TARs created in the current year
                int yearlyCount = db.TravelExpenses
                    .Count(t => t.CreatedDate.Year == currentYear) + 1;

                // Format with leading zeros
                string tarNo = $"TAR-{currentYear}-{yearlyCount.ToString("D3")}";
                return tarNo;
            }
        }

        public static (string requesterSign, DateTime? requesterSignDate, TravelExpenseApproval approverApproval) GetLatestRequesterPreloadInfo(string username)
        {
            using (var db = new FinancePortalEntities())
            {
                // Get latest request created by this user
                var latestExpense = db.TravelExpenses
                    .Where(t => t.CreatedBy == username)
                    .OrderByDescending(t => t.CreatedDate)
                    .FirstOrDefault();

                if (latestExpense == null)
                    return (null, null, null);

                // Find the associated approver (HOD) info
                var approverApproval = db.TravelExpenseApprovals
                    .FirstOrDefault(a => a.TravelExpenseID == latestExpense.ID && a.ApprovalStep == (int)ApprovalStep.HOD);

                return (
                    requesterSign: latestExpense.RequesterSignature,
                    requesterSignDate: latestExpense.CreatedDate,
                    approverApproval: approverApproval
                );
            }
        }

        public static ApproverViewModel GetHodUserByCode(string code)
        {
            using (var db = new FinancePortalEntities())
            {
                var user = db.Users.FirstOrDefault(u => u.EmployeeCode == code && u.IsActive && u.IsShown);
                if (user == null) return null;

                var roleNames = (from ur in db.UserRoles
                                 join r in db.Roles on ur.RoleId equals r.RoleId
                                 where ur.UserId == user.UserId && ur.IsShown == true && r.RoleName == HODRole
                                 select r.RoleName).ToList();

                if (!roleNames.Any()) return null;

                return new ApproverViewModel
                {
                    Code = user.EmployeeCode,
                    Name = user.UserName,
                    Email = user.UserEmailAddress,
                    Position = "(HOD)" // or load from Employee table if needed
                };
            }
        }

        // DAO Method
        public static bool ApproveByHOD(int travelExpenseId, string approverID, string approverName, out string message)
        {
            message = string.Empty;

            using (var db = new FinancePortalEntities())
            {
                var travel = db.TravelExpenses.FirstOrDefault(t => t.ID == travelExpenseId && t.IsShown);
                if (travel == null)
                {
                    message = "Travel request not found.";
                    return false;
                }

                if (travel.StatusID != (int)TravelExpenseStatusEnum.WaitingHOD)
                {
                    message = "Request is not in HOD Pending status.";
                    return false;
                }

                var approval = db.TravelExpenseApprovals.FirstOrDefault(a => a.TravelExpenseID == travelExpenseId && a.ApprovalStep == (int)ApprovalStep.HOD && a.IsApproved == false);
                if (approval == null)
                {
                    message = "HOD approval record not found or already approved.";
                    return false;
                }

                // Update approval
                approval.IsApproved = true;
                approval.ApproverSignDate = DateTime.Now;
                approval.ApproverSignature = approverName;
                approval.UpdatedBy = approverName;
                approval.UpdatedDate = DateTime.Now;

                // Update travel expense status
                travel.StatusID = (int)TravelExpenseStatusEnum.WaitingGL;
                travel.UpdatedBy = approverName;
                travel.UpdatedDate = DateTime.Now;

                db.SaveChanges();
                return true;
            }
        }

        #endregion

        #region Expense Budget

        public static bool AddBudget(BudgetViewModel model, out string message)
        {
            message = string.Empty;

            using (var db = new FinancePortalEntities())
            {
                // Normalize name
                string nameToCheck = model.BudgetName?.Trim().ToLower();

                // 🔁 Duplicate check (shown budgets, excluding self on edit)
                bool isDuplicate = db.TravelExpenseBudgets.Any(b =>
                    b.BudgetName.Trim().ToLower() == nameToCheck &&
                    b.IsShown &&
                    b.ID != model.BudgetID);

                if (isDuplicate)
                {
                    message = "Budget name already exists.";
                    return false;
                }

                if (model.BudgetID == 0)
                {
                    // ➕ Add new
                    var newBudget = new TravelExpenseBudget
                    {
                        BudgetName = model.BudgetName?.Trim(),
                        BudgetAmount = model.BudgetAmount,
                        BudgetUsed = 0,
                        BudgetRemaining = model.BudgetAmount,
                        CreatedDate = DateTime.Now,
                        CreatedBy = System.Web.HttpContext.Current?.Session["Username"]?.ToString(),
                        IsShown = true
                    };

                    db.TravelExpenseBudgets.Add(newBudget);
                    db.SaveChanges();

                    // Add TravelExpenseCostBudget
                    foreach (var costID in model.CostIDList)
                    {
                        var costBudget = new TravelExpenseCostBudget
                        {
                            CostID = costID,
                            BudgetID = newBudget.ID,
                            IsShown = true
                        };
                        db.TravelExpenseCostBudgets.Add(costBudget);
                        db.SaveChanges();
                    }
                    
                }
                else
                {
                    // ✏️ Update existing
                    var existing = db.TravelExpenseBudgets.FirstOrDefault(b => b.ID == model.BudgetID);
                    if (existing == null)
                    {
                        message = "Budget not found.";
                        return false;
                    }

                    existing.BudgetName = model.BudgetName?.Trim();
                    existing.BudgetAmount = model.BudgetAmount;
                    existing.BudgetRemaining = model.BudgetAmount - existing.BudgetUsed;
                    existing.UpdatedDate = DateTime.Now;
                    existing.UpdatedBy = System.Web.HttpContext.Current?.Session["Username"]?.ToString();
                }

                db.SaveChanges();
                return true;
            }
        }

        public static List<TravelExpenseBudgetViewModel> GetAllBudgets()
        {
            using (var db = new FinancePortalEntities())
            {
                return db.TravelExpenseBudgets
                    .OrderByDescending(b => b.CreatedDate)
                    .Select(b => new TravelExpenseBudgetViewModel
                    {
                        ID = b.ID,
                        BudgetName = b.BudgetName,
                        BudgetAmount = b.BudgetAmount,
                        BudgetUsed = b.BudgetUsed,
                        BudgetRemaining = b.BudgetRemaining,
                        CreatedDate = b.CreatedDate,
                        CreatedBy = b.CreatedBy,
                        IsShown = b.IsShown
                    })
                    .ToList();
            }
        }

        public static List<TravelExpenseBudgetViewModel> GetShownBudgets()
        {
            using (var db = new FinancePortalEntities())
            {
                return db.TravelExpenseBudgets
                    .Where(b => b.IsShown)
                    .OrderByDescending(b => b.CreatedDate)
                    .Select(b => new TravelExpenseBudgetViewModel
                    {
                        ID = b.ID,
                        BudgetName = b.BudgetName,
                        BudgetAmount = b.BudgetAmount,
                        BudgetUsed = b.BudgetUsed,
                        BudgetRemaining = b.BudgetRemaining,
                        CreatedDate = b.CreatedDate,
                    })
                    .ToList();
            }
        }

        //Get all cost budget to show drop down
        public static List<CostBudgetInfoViewModel> GetCostBudgetList()
        {
            using (var db = new FinancePortalEntities())
            {
                var costBudget = (from cb in db.TravelExpenseCostBudgets
                                  join c in db.TravelExpenseCosts on cb.CostID equals c.ID
                                  join b in db.TravelExpenseBudgets on cb.BudgetID equals b.ID
                                  where cb.IsShown == true
                                  select new CostBudgetInfoViewModel
                                  {
                                      ID = cb.ID,
                                      CostID = cb.CostID,
                                      CostName = c.CostName,
                                      BudgetID = cb.BudgetID,
                                      BudgetName = b.BudgetName,
                                      BudgetRemaining = b.BudgetRemaining
                                  }).ToList();
                return costBudget;
            }
        }

        public static object GetBudgetDetailsById(int id)
        {
            using (var db = new FinancePortalEntities())
            {
                var budget = db.TravelExpenseBudgets
                    .Where(b => b.ID == id && b.IsShown)
                    .Select(b => new
                    {
                        b.ID,
                        b.BudgetName,
                        b.BudgetAmount,
                        b.BudgetUsed,
                        b.BudgetRemaining
                    })
                    .FirstOrDefault();

                return budget;
            }
        }

        public static List<CostBudgetInfoViewModel> GetBudgetDetailByCostBudget(List<int> costBudgetIDList)
        {
            using (var db = new FinancePortalEntities())
            {
                var list = (from cb in db.TravelExpenseCostBudgets
                            join b in db.TravelExpenseBudgets on cb.BudgetID equals b.ID
                            where cb.IsShown == true
                                  && costBudgetIDList.Contains(cb.ID)
                            select new CostBudgetInfoViewModel
                            {
                                ID = cb.ID,
                                BudgetID = b.ID,
                                BudgetName = b.BudgetName,
                                BudgetAmount = b.BudgetAmount,
                                BudgetRemaining = b.BudgetRemaining,
                                BudgetUsed = b.BudgetUsed
                            }).ToList();

                return list;
            }
        }

        public static BudgetViewModel GetBudgetInfoByCostBudget(int costBudgetID)
        {
            using (var db = new FinancePortalEntities())
            {
                var budgetResult = (from cb in db.TravelExpenseCostBudgets
                                    join b in db.TravelExpenseBudgets on cb.BudgetID equals b.ID
                                    where cb.IsShown == true
                                          && costBudgetID == cb.ID
                                    select new BudgetViewModel
                                    {
                                        BudgetID = b.ID,
                                        BudgetName = b.BudgetName,
                                        BudgetAmount = b.BudgetAmount,
                                        BudgetRemaining = b.BudgetRemaining,
                                        BudgetUsed = b.BudgetUsed
                                    }).FirstOrDefault();
                return budgetResult;
            }
        }

     
        #endregion

        #region Dashboard

        public static object GetDashboardStats()
        {
            using (var db = new FinancePortalEntities())
            {
                // Budget stats
                decimal used = db.TravelExpenseBudgets
                                 .Where(b => b.IsShown)
                                 .Sum(b => (decimal?)b.BudgetUsed) ?? 0;

                decimal remaining = db.TravelExpenseBudgets
                                      .Where(b => b.IsShown)
                                      .Sum(b => (decimal?)b.BudgetRemaining) ?? 0;

                // Get count of TravelExpenses grouped by StatusID
                var statusCounts = db.TravelExpenses
                    .GroupBy(t => t.StatusID)
                    .Select(g => new { StatusID = g.Key, Count = g.Count() })
                    .ToList();

                // Load all defined statuses
                var statusMap = db.TravelExpenseStatus.ToList();

                // Combine status display name + color + count
                var statusResults = statusMap.Select(s => new
                {
                    label = s.DisplayName,
                    value = statusCounts.FirstOrDefault(c => c.StatusID == s.ID)?.Count ?? 0,
                    color = s.ColorCode
                }).ToList();

                return new
                {
                    used,
                    remaining,
                    statuses = statusResults
                };
            }
        }

        #endregion

        #region Request List

        public static List<object> GetRequestSummariesByUser(string username, string employeeCode, string role)
        {
            int maxItems = int.TryParse(ConfigurationManager.AppSettings["MaxRequestListItems"], out int limit)
                            ? limit
                            : 100; // Default fallback

            using (var db = new FinancePortalEntities())
            {

                var query = (
                     from t in db.TravelExpenses
                     join s in db.TravelExpenseStatus on t.StatusID equals s.ID
                     join u in db.Users on t.CreatedBy equals u.UserName
                     where t.IsShown == true &&
                         (
                             (role == RequesterRole && t.CreatedBy == username) ||
                             (role == HODRole && db.TravelExpenseApprovals.Any(a =>
                                 a.TravelExpenseID == t.ID &&
                                 a.ApprovalStep == 1 &&
                                 a.ApproverID == employeeCode)) ||
                             (role == FCRole && db.TravelExpenseApprovals.Any(a =>
                                 a.TravelExpenseID == t.ID &&
                                 a.ApprovalStep == 3 &&
                                 a.ApproverID == employeeCode)) ||
                             (!new[] { RequesterRole, HODRole, FCRole }.Contains(role))
                         )
                     orderby t.RequestDate descending
                     select new
                                 {
                                     t.ID,
                                     u.Department,
                                     t.TarNo,
                                     t.TripPurpose,
                                     t.RequestDate,
                                     t.EstimatedCost,
                                     t.CreatedBy,
                                     s.DisplayName,
                                     s.ColorCode,
                                     EditMode =
                                            (role == RequesterRole && (
                                                t.StatusID == (int)TravelExpenseStatusEnum.WaitingHOD ||
                                                t.StatusID == (int)TravelExpenseStatusEnum.RejectedHOD ||
                                                t.StatusID == (int)TravelExpenseStatusEnum.RejectedFC)) ? 1 :
                                            (role == GLRole && t.StatusID < 7) ? 1 : 0,
                                     NewCash =
                                            (role == RequesterRole &&
                                                 t.StatusID == (int)TravelExpenseStatusEnum.TARApproved &&
                                                 !db.CashInAdvances.Any(c => c.TravelExpenseID == t.ID)) ? 1 : 0,
                                     EditCash = db.CashInAdvances.Any(c => c.TravelExpenseID == t.ID) ? 1 : 0,
                     }

                 ).ToList(); // Materialize here

                // Now project with encryption
                var result = query.Select(x => new
                {
                    x.ID,
                    Token = TokenHelper.Encrypt(x.ID.ToString()),
                    x.Department,
                    x.TarNo,
                    x.TripPurpose,
                    x.RequestDate,
                    x.EstimatedCost,
                    x.CreatedBy,
                    x.DisplayName,
                    x.ColorCode,
                    x.EditMode,
                    x.NewCash,
                    x.EditCash
                });

                return result.ToList<object>();



                //List<object> result = db.Database.SqlQuery<RequestSummariesByUserResult>(
                //    "EXEC GetRequestSummariesByUser @Role, @Username, @EmployeeCode",
                //    new SqlParameter("@Role", role),
                //    new SqlParameter("@Username", username),
                //    new SqlParameter("@EmployeeCode", employeeCode)
                //).Take(maxItems)
                // .ToList<object>();
                //return result;

            }
        }

        public static List<string> GetAllStatus()
        {
            using (var db = new FinancePortalEntities())
            {
                var list = db.TravelExpenseStatus.Select(item => item.DisplayName).ToList();
                return list;
            }
        }

        public static List<CostViewModel> GetAllCosts(int budgetID)
        {
            using (var db = new FinancePortalEntities())
            {
                var costInitList = db.TravelExpenseCosts
                    .Select(item => new CostViewModel
                    {
                        CostID = item.ID,
                        CostName = item.CostName,
                        Checked = false,
                    }).ToList();

                if (budgetID > 0)
                {
                    var selectedCostIDs = db.TravelExpenseCostBudgets
                        .Where(cb => cb.BudgetID == budgetID)
                        .Select(cb => cb.CostID)
                        .ToHashSet(); // Efficient lookup

                    foreach (var cost in costInitList)
                    {
                        if (selectedCostIDs.Contains(cost.CostID))
                        {
                            cost.Checked = true;
                        }
                    }
                }
                return costInitList.OrderBy(item => item.CostID).ToList();
            }
        }

        public static TravelExpenseViewModel GetRequestViewById(int id)
        {
            using (var db = new FinancePortalEntities())
            {
                var t = db.TravelExpenses.FirstOrDefault(x => x.ID == id);
                if (t == null) return null;

                var s = db.TravelExpenseStatus.FirstOrDefault(x => x.ID == t.StatusID);

                var approvals = db.TravelExpenseApprovals
                                  .Where(a => a.TravelExpenseID == t.ID)
                                  .OrderBy(a => a.ApprovalStep)
                                  .Select(a => new ApprovalInfoViewModel
                                  {
                                      ApprovalStep = a.ApprovalStep,
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

                var costDetails = (from cd in db.TravelExpenseCostDetails
                                   join cb in db.TravelExpenseCostBudgets on cd.CostBudgetID equals cb.ID
                                   join c in db.TravelExpenseCosts on cb.CostID equals c.ID
                                   join b in db.TravelExpenseBudgets on cb.BudgetID equals b.ID
                                   where
                                    cd.TravelExpenseID == t.ID && cd.IsShown == true
                                   select new TravelExpenseCostDetailViewModel
                                   {
                                       CostName = c.CostName,
                                       CostAmount = cd.CostAmount,
                                       BudgetName = b.BudgetName,
                                       BudgetAmountAtSubmit = cd.BudgetAmountAtSubmit,
                                       BudgetRemainAtSubmit = cd.BudgetRemainingAtSubmit,
                                       BudgetUsedAtSubmit = cd.BudgetUsedAtSubmit,
                                   }).ToList();

                var model = new TravelExpenseViewModel
                {
                    ID = t.ID,
                    FromDate = t.FromDate,
                    ToDate = t.ToDate,
                    TripDays = t.TripDays,
                    RequestDate = t.RequestDate,
                    TarNo = t.TarNo,
                    TripPurpose = t.TripPurpose,
                    EstimatedCost = t.EstimatedCost,
                    ExchangeRate = t.ExchangeRate,
                    RequesterSign = t.RequesterSignature,
                    CreatedDate = t.CreatedDate,

                    StatusID = t.StatusID,
                    StatusName = s?.DisplayName,
                    StatusColor = s?.ColorCode,

                    CostDetails= costDetails,
                    Employees = db.TravelExpenseEmployees
                                 .Where(e => e.TravelExpenseID == t.ID && e.IsShown)
                                 .Select(e => new EmployeeViewModel
                                 {
                                     Code = e.EmployeeCode,
                                     Name = e.FullName,
                                     Position = e.Position,
                                     Department = e.Department,
                                     Division = e.Division
                                 }).ToList(),


                    Approvals = approvals.OrderBy(a => a.ApprovalStep).ToList(),
                    AttachmentFiles = db.TravelExpenseAttachmentFiles
                                    .Where(a => a.TravelExpenseID == t.ID && a.IsShown == true)
                                    .Select(a => a.FileName).ToList(),
                };

                return model;
            }
        }

        public static TravelExpenseSubmitModel GetSubmitModelById(int id)
        {
            using (var db = new FinancePortalEntities())
            {
                var t = db.TravelExpenses.FirstOrDefault(x => x.ID == id);
                if (t == null) return null;

                var approver = db.TravelExpenseApprovals
                                .Where(a => a.TravelExpenseID == t.ID && a.ApprovalStep == (int)ApprovalStep.HOD)
                                .Select(a => new ApproverViewModel
                                {
                                    Code = a.ApproverID,
                                    Name = a.ApproverName,
                                    Position = a.ApproverPosition,
                                    Email = a.ApproverEmail
                                })
                                .FirstOrDefault();

                var employees = db.TravelExpenseEmployees
                                  .Where(e => e.TravelExpenseID == t.ID && e.IsShown)
                                  .Select(e => new EmployeeViewModel
                                  {
                                      Code = e.EmployeeCode,
                                      Name = e.FullName,
                                      Position = e.Position,
                                      Department = e.Department,
                                      Division = e.Division
                                  }).ToList();

                var costDetails = db.TravelExpenseCostDetails
                             .Where(c => c.TravelExpenseID == t.ID && c.IsShown == true)
                             .Select(c => new TravelExpenseCostDetailViewModel
                             {
                                 CostBudgetID = c.CostBudgetID,
                                 CostAmount = c.CostAmount,
                             }).ToList() ?? new List<TravelExpenseCostDetailViewModel>();

                var files = db.TravelExpenseAttachmentFiles
                            .Where(c => c.TravelExpenseID.Equals(t.ID) && c.IsShown == true && c.Type == (int)TypeAttachmentFile.TravelExpense)
                            .Select(c => c.FileName).ToList();

                return new TravelExpenseSubmitModel
                {
                    ID = t.ID,
                    StatusID = t.StatusID,
                    FromDate = t.FromDate,
                    ToDate = t.ToDate,
                    TripDays = t.TripDays,
                    RequestDate = t.RequestDate,
                    TarNo = t.TarNo,
                    TripPurpose = t.TripPurpose,
                    EstimatedCost = t.EstimatedCost,
                    ExchangeRate = t.ExchangeRate,
                    RequesterSign = t.RequesterSignature,
                    CreatedDate = t.CreatedDate,
                    AttachmentFiles = files,
                    CostDetails = costDetails,
                    Employees = employees,
                    Approver = approver
                };
            }
        }

        public static OperationResult HandleHODApproval(int requestId, string approverUsername, bool isApprove)
        {
            using (var db = new FinancePortalEntities())
            {
                var now = DateTime.Now;

                var approval = db.TravelExpenseApprovals
                    .FirstOrDefault(a => a.TravelExpenseID == requestId && a.ApprovalStep == (int)ApprovalStep.HOD);

                if (approval == null)
                    return new OperationResult { Success = false, Message = "HOD approval record not found." };

                var glUser = db.Users
                            .Where(u => u.UserRoles.Any(r => r.Role.RoleName == GLRole && r.IsShown == true))
                            .FirstOrDefault();

                if (isApprove)
                {
                    if (glUser == null)
                        return new OperationResult { Success = false, Message = "No GL is assigned. Cannot proceed with approval." };
                }

                approval.ApproverSignature = approverUsername;
                approval.ApproverSignDate = now;
                approval.IsApproved = isApprove;
                approval.UpdatedDate = now;
                approval.UpdatedBy = approverUsername;

                var request = db.TravelExpenses.FirstOrDefault(t => t.ID == requestId && t.IsShown);
                if (request == null)
                    return new OperationResult { Success = false, Message = "Travel request not found." };

                request.StatusID = isApprove
                    ? (int)TravelExpenseStatusEnum.WaitingGL
                    : (int)TravelExpenseStatusEnum.RejectedHOD;
                request.UpdatedBy = approverUsername;
                request.UpdatedDate = now;

                var userRequester = db.Users.FirstOrDefault(item => item.UserName == request.CreatedBy);
                string mailRequester = userRequester?.UserEmailAddress ?? string.Empty;
                var mailContent = new MailContentModel();
                mailContent.TarNumber = request.TarNo;

                if (isApprove)
                {
                    var glApprovalExists = db.TravelExpenseApprovals
                        .Any(a => a.TravelExpenseID == requestId && a.ApprovalStep == (int)ApprovalStep.GL);

                    if (!glApprovalExists)
                    {
                        if (glUser != null)
                        {
                            db.TravelExpenseApprovals.Add(new TravelExpenseApproval
                            {
                                TravelExpenseID = requestId,
                                ApprovalStep = (int)ApprovalStep.GL,
                                ApproverID = glUser.EmployeeCode,
                                ApproverName = glUser.UserName,
                                ApproverEmail = glUser.UserEmailAddress,
                                ApproverPosition = "GL Dept",
                                IsApproved = false,
                                CreatedDate = now,
                                CreatedBy = approverUsername
                            });

                            //Approve:Send mail to GL
                            mailContent.RecipientTo = glUser.UserEmailAddress;
                            mailContent.RecipientCc = mailRequester;
                            mailContent.Content = "Please be informed that you have a travel expense request waiting for approval.";
                            SendEmail(mailContent);
                        }
                    }
                }

                //Reject:Send mail to Requester
                mailContent.RecipientTo = mailRequester;
                mailContent.Content = "Please be informed that your travel expense request has been <strong>denied</strong> by HOD.";
                SendEmail(mailContent);

                db.SaveChanges();
                return new OperationResult
                {
                    Success = true,
                    Message = isApprove ? "Request approved successfully." : "Request rejected successfully."
                };
            }
        }

        public static OperationResult HandleGLApproval(int requestId, string approverUsername, bool isApprove)
        {
            using (var db = new FinancePortalEntities())
            {
                var now = DateTime.Now;

                var request = db.TravelExpenses.FirstOrDefault(t => t.ID == requestId && t.IsShown);
                if (request == null)
                    return new OperationResult { Success = false, Message = "Request not found." };

                var approval = db.TravelExpenseApprovals
                    .FirstOrDefault(a => a.TravelExpenseID == requestId && a.ApprovalStep == (int)ApprovalStep.GL);
                if (approval == null)
                    return new OperationResult { Success = false, Message = "GL Approval record not found." };

                var fcUser = db.Users
                            .Where(u => u.UserRoles.Any(r => r.Role.RoleName == FCRole && r.IsShown == true))
                            .FirstOrDefault();

                // Update approval
                approval.ApproverSignature = approverUsername;
                approval.ApproverSignDate = now;
                approval.IsApproved = isApprove;
                approval.UpdatedDate = now;
                approval.UpdatedBy = approverUsername;

                request.StatusID = (int)TravelExpenseStatusEnum.WaitingFC;
                request.UpdatedBy = approverUsername;
                request.UpdatedDate = now;

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
                    var fcApprovalExists = db.TravelExpenseApprovals
                        .Any(a => a.TravelExpenseID == requestId && a.ApprovalStep == (int)ApprovalStep.FC);

                    if (!fcApprovalExists)
                    {
                        if (fcUser != null)
                        {
                            db.TravelExpenseApprovals.Add(new TravelExpenseApproval
                            {
                                TravelExpenseID = requestId,
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

                    //Approve:Send mail to FC
                    var userRequester = db.Users.FirstOrDefault(item => item.UserName == request.CreatedBy);
                    string mailRequester = userRequester?.UserEmailAddress ?? string.Empty;

                    var mailContent = new MailContentModel();
                    mailContent.TarNumber = request.TarNo;
                    mailContent.RecipientTo = fcUser.UserEmailAddress;
                    mailContent.RecipientCc = mailRequester;
                    mailContent.Content = "Please be informed that you have a travel expense request waiting for approval.";
                    SendEmail(mailContent);
                }

                db.SaveChanges();

                return new OperationResult
                {
                    Success = true,
                    Message = isApprove ? "Request approved successfully." : "Request rejected successfully."
                };
            }
        }

        public static OperationResult HandleFCApproval(int requestId, string approverUsername, bool isApprove)
        {
            using (var db = new FinancePortalEntities())
            {
                var now = DateTime.Now;

                var request = db.TravelExpenses.FirstOrDefault(t => t.ID == requestId && t.IsShown);
                if (request == null)
                    return new OperationResult { Success = false, Message = "Request not found." };

                var approval = db.TravelExpenseApprovals
                    .FirstOrDefault(a => a.TravelExpenseID == requestId && a.ApprovalStep == (int)ApprovalStep.FC);
                if (approval == null)
                    return new OperationResult { Success = false, Message = "FC Approval record not found." };

                // Update approval
                approval.ApproverSignature = approverUsername;
                approval.ApproverSignDate = now;
                approval.IsApproved = isApprove;
                approval.UpdatedDate = now;
                approval.UpdatedBy = approverUsername;

                request.StatusID = isApprove
                    ? (int)TravelExpenseStatusEnum.TARApproved
                    : (int)TravelExpenseStatusEnum.RejectedFC;
                request.UpdatedBy = approverUsername;
                request.UpdatedDate = now;

                db.SaveChanges();

                //Approve:Send mail to Requester
                var userRequester = db.Users.FirstOrDefault(item => item.UserName == request.CreatedBy);
                string mailRequester = userRequester?.UserEmailAddress ?? string.Empty;

                var mailContent = new MailContentModel();
                mailContent.TarNumber = request.TarNo;
                mailContent.RecipientTo = mailRequester;
                if (isApprove)
                {
                    mailContent.Content = "Please be informed that your travel expense request has been done.";
                }
                else
                {
                    mailContent.Content = "Please be informed that your travel expense request has been <strong>denied</strong> by FC.";
                }
                SendEmail(mailContent);

                return new OperationResult
                {
                    Success = true,
                    Message = isApprove ? "Request approved successfully." : "Request rejected successfully."
                };
            }
        }

        public static OperationResult HandleRequesterCancel(int requestId, string approverUsername)
        {
            using (var db = new FinancePortalEntities())
            {
                var now = DateTime.Now;

                var request = db.TravelExpenses.FirstOrDefault(t => t.ID == requestId && t.IsShown);
                if (request == null)
                    return new OperationResult { Success = false, Message = "Request not found." };

                request.StatusID = (int)TravelExpenseStatusEnum.Cancelled;
                request.UpdatedBy = approverUsername;
                request.UpdatedDate = now;

                db.SaveChanges();

                return new OperationResult
                {
                    Success = true,
                    Message = "Request cancel successfully."
                };
            }
        }

        public static string GetEmailContent(MailContentModel mailContent)
        {
            string content = $@"
                    <!DOCTYPE html>
                    <html>
                    <head>
                      <meta charset=""UTF-8"">
                      <style>
                        body {{
                          font-family: Arial, sans-serif;
                          color: #333;
                          background-color: #f0f2f5;
                          margin: 0;
                          padding: 0;
                        }}
                        .container {{
                          padding: 20px;
                          max-width: 600px;
                          margin: 30px auto;
                          background-color: #ffffff;
                          border-radius: 8px;
                          border: 1px solid #ddd;
                          box-shadow: 0 2px 8px rgba(0,0,0,0.05);
                        }}
                        .header {{
                          background-color: #ffc720;
                          color: #ffffff;
                          padding: 15px;
                          text-align: center;
                          font-size: 22px;
                          font-weight: bold;
                          border-radius: 6px 6px 0 0;
                        }}
                        .content {{
                          padding: 20px;
                          font-size: 16px;
                          line-height: 1.6;
                        }}
                        .content a {{
                          color: #0275d8;
                          text-decoration: none;
                        }}
                        .footer {{
                          margin-top: 30px;
                          font-size: 12px;
                          color: #777;
                          text-align: center;
                          border-top: 1px solid #eee;
                          padding-top: 10px;
                        }}
                        .padding-text {{
                            padding: 20px
                        }}
                      </style>
                    </head>
                    <body>
                      <div class=""container"">
                        <div class=""header"">
                          TAR NO: {mailContent.TarNumber}
                        </div>
                        <div class=""content"">
                          <br>
                          <br>
                          <p>&nbsp;&nbsp;&nbsp;&nbsp;Dear,</p>

                          <p>&nbsp;&nbsp;&nbsp;&nbsp;{mailContent.Content}</p>

                          <p>&nbsp;&nbsp;&nbsp;&nbsp;You can view the details or take further action by clicking the link below:</p>
                          <p>&nbsp;&nbsp;&nbsp;&nbsp;<a href=""http://localhost:18947/"" target=""_blank"">View Travel Expense Request</a></p>

                          <p>&nbsp;&nbsp;&nbsp;&nbsp;Thank you.</p>

                          <p>&nbsp;&nbsp;&nbsp;&nbsp;Best regards,<br></p>
                          <br>
                          <br>
                        </div>
                        <div class=""footer"">
                          &nbsp;&nbsp;&nbsp;&nbsp;This is an automated message. Please do not reply directly to this email.
                        </div>
                      </div>
                    </body>
                    </html>
                    ";

            return content;
        }

        public static void SendEmail(MailContentModel mailContent)
        {
            string emailSender = "travelexpense@thegrandhotram.com";
            string bodyContent = GetEmailContent(mailContent);
            try
            {
                string emailSubject = ConfigurationManager.AppSettings["EmailSubject"];
                string smtpClientName = ConfigurationManager.AppSettings["SMTPClientName"];

                MailMessage message = new MailMessage();
                SmtpClient smtpClient = new SmtpClient(smtpClientName);

                message.From = new MailAddress(emailSender);
                message.To.Add(mailContent.RecipientTo);
                message.CC.Add(mailContent.RecipientCc);
                message.Subject = emailSubject;
                message.IsBodyHtml = true;
                message.Body = bodyContent;

                smtpClient.Port = 25;

                smtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;
                smtpClient.UseDefaultCredentials = false;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls
                                      | SecurityProtocolType.Tls11
                                      | SecurityProtocolType.Tls12;
                smtpClient.Timeout = 100000;

                smtpClient.Send(message);
                message.Attachments.Dispose();


            }
            catch (Exception ex)
            {

            }
        }


        //public static List<object> GetAllRequest(string username, string employeeCode, string role)
        //{
        //    int maxItems = int.TryParse(ConfigurationManager.AppSettings["MaxRequestListItems"], out int limit)
        //                    ? limit
        //                    : 100; // Fallback default

        //    using (var db = new FinancePortalEntities())
        //    {
        //        // Step 1: Prepare allowed travel expense IDs based on role logic (one-time shared condition)
        //        var travelExpensesQuery = db.TravelExpenses.Where(t => t.IsShown);

        //        if (role == RequesterRole)
        //        {
        //            travelExpensesQuery = travelExpensesQuery.Where(t => t.CreatedBy == username);
        //        }
        //        else if (role == HODRole)
        //        {
        //            travelExpensesQuery = travelExpensesQuery.Where(t =>
        //                db.TravelExpenseApprovals.Any(a =>
        //                    a.TravelExpenseID == t.ID &&
        //                    a.ApprovalStep == 1 &&
        //                    a.ApproverID == employeeCode));
        //        }
        //        else if (role == FCRole)
        //        {
        //            travelExpensesQuery = travelExpensesQuery.Where(t =>
        //                db.TravelExpenseApprovals.Any(a =>
        //                    a.TravelExpenseID == t.ID &&
        //                    a.ApprovalStep == 3 &&
        //                    a.ApproverID == employeeCode));
        //        }

        //        // TravelExpense IDs that match permission logic
        //        var travelIDs = travelExpensesQuery.Select(t => t.ID);

        //        // Step 2: Identify which of these are in CashInAdvance
        //        var travelIDInCIA = db.CashInAdvances
        //            .Where(c => c.IsShown && travelIDs.Contains(c.TravelExpenseID))
        //            .Select(c => c.TravelExpenseID)
        //            .Distinct();

        //        // Step 3: Get Travel IDs not in CIA (those still only in TravelExpenses)
        //        var travelIDOnlyInTravel = travelIDs.Except(travelIDInCIA);

        //        // Step 4: Fetch detailed records for each group (with joins)
        //        var travelOnlyList = (from t in db.TravelExpenses
        //                              join s in db.TravelExpenseStatus on t.StatusID equals s.ID
        //                              join u in db.Users on t.CreatedBy equals u.UserName
        //                              where travelIDOnlyInTravel.Contains(t.ID)
        //                              select new
        //                              {
        //                                  u.Department,
        //                                  t.ID,
        //                                  t.TarNo,
        //                                  s.DisplayName,
        //                                  s.ColorCode
        //                              }).OrderByDescending(i => i.ID).ToList<object>();

        //        var travelWithCIAList = (from c in db.CashInAdvances
        //                                 join t in db.TravelExpenses on c.TravelExpenseID equals t.ID
        //                                 join s in db.TravelExpenseStatus on t.StatusID equals s.ID
        //                                 join u in db.Users on t.CreatedBy equals u.UserName
        //                                 where travelIDInCIA.Contains(t.ID)
        //                                 select new
        //                                 {
        //                                     u.Department,
        //                                     t.ID,
        //                                     t.TarNo,
        //                                     s.DisplayName,
        //                                     s.ColorCode
        //                                 }).OrderByDescending(i => i.ID).ToList<object>();

        //        // Step 5: Combine and return
        //        return travelOnlyList.Concat(travelWithCIAList).ToList();
        //    }
        //}

        public static List<TravelInfoViewModel> GetCurrentList(string role, int travelID)
        {
            using (var db = new FinancePortalEntities())
            {

                var cashQuery = (from c in db.CashInAdvances
                                 where c.TravelExpenseID == travelID
                                 select new
                                 {
                                     ID = c.TravelExpenseID ,
                                     c.StatusID,
                                     c.CreatedDate
                                 }).FirstOrDefault();

                var travelList = new List<TravelInfoViewModel>();

                if (cashQuery != null)
                {
                    var cashInfo = new TravelInfoViewModel
                    {
                        FormName = "Cash In Advance",
                        TokenID = TokenHelper.Encrypt(cashQuery.ID.ToString()),
                        CreatedDate = cashQuery.CreatedDate.ToString("dd-MM-yyyy"),
                        ID = cashQuery.ID,
                        EditMode = ((role == RequesterRole && (
                                        cashQuery.StatusID == (int)TravelExpenseStatusEnum.WaitingHOD ||
                                        cashQuery.StatusID == (int)TravelExpenseStatusEnum.RejectedHOD ||
                                        cashQuery.StatusID == (int)TravelExpenseStatusEnum.RejectedFC))
                                    || (role == GLRole && cashQuery.StatusID < 7)) ? 1 : 0,
                        CashMode = (role == RequesterRole && cashQuery.StatusID == (int)TravelExpenseStatusEnum.TARApproved) ? 1 : 0
                    };
                    travelList.Add(cashInfo);
                }

                return travelList;
            }
        }

        #endregion
    }
}