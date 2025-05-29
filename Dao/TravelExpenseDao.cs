using FinancePortal.Models;
using FinancePortal.ViewModels;
using Microsoft.AspNet.Identity;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Data.Entity.Validation;
using System.Linq;
using System.Web;

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
                var result = (from e in context.Employees
                              join d in context.Divisions on e.DivisionID equals d.DivisionID into div
                              from d in div.DefaultIfEmpty()
                              where e.EmployeeID == code
                              select new EmployeeViewModel
                              {
                                  Code = e.EmployeeID,
                                  Name = e.FullName,
                                  Position = e.Position,
                                  Department = e.DepartmentName,
                                  Division = d != null ? d.DivisionName : "(No Division)",
                                  Email = e.EmailAddress
                              }).FirstOrDefault();

                return result;
            }
        }

        public static bool SaveTravelExpense(TravelExpenseSubmitModel model)
        {
            using (var db = new FinancePortalEntities())
            {
                var username = HttpContext.Current.Session["Username"]?.ToString();
                var employeeID = HttpContext.Current.Session["EmployeeID"]?.ToString();
                var email = HttpContext.Current.Session["Email"]?.ToString();
                var position = HttpContext.Current.Session["Position"]?.ToString();

                // 1. Validate Budget
                var budget = db.TravelExpenseBudgets.FirstOrDefault(b => b.ID == model.BudgetID && b.IsShown);
                if (budget == null)
                    throw new Exception("Selected budget not found or inactive.");

                if (budget.BudgetRemaining < model.EstimatedCost)
                    throw new Exception("Estimated cost exceeds remaining budget.");

                // 2. Create TravelExpense main record
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
                    BudgetID = model.BudgetID,
                    BudgetAmountAtSubmit = budget.BudgetAmount,
                    BudgetUsedAtSubmit = budget.BudgetUsed,
                    BudgetRemainingAtSubmit = budget.BudgetRemaining,
                    TarNo = model.TarNo,
                    IsShown = true,
                    StatusID = (int)TravelExpenseStatusEnum.RequesterPending // 🛑 NEW: Initial status
                };

                db.TravelExpenses.Add(travel);
                db.SaveChanges(); // get travel.ID

                // 3. Insert HOD Approval Step 
                if (model.Approver != null)
                {
                    db.TravelExpenseApprovals.Add(new TravelExpenseApproval
                    {
                        TravelExpenseID = travel.ID,
                        ApprovalStep = (int)ApprovalStep.HOD,
                        ApproverID = model.Approver.Code,
                        ApproverName = model.Approver.Name,
                        ApproverEmail = model.Approver.Email,
                        ApproverPosition = model.Approver.Position,
                        IsApproved = false, // pending
                        CreatedBy = username,
                        CreatedDate = DateTime.Now
                    });
                }
                else
                {
                    throw new Exception("Approver (HOD) information is required.");
                }

                // 4. Insert Employees
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

                // 5. Insert Cost Details
                if (model.CostDetails != null)
                {
                    db.TravelExpenseCosts.Add(new TravelExpenseCost
                    {
                        TravelExpenseID = travel.ID,
                        CostAir = model.CostDetails.CostAir,
                        CostHotel = model.CostDetails.CostHotel,
                        CostMeal = model.CostDetails.CostMeal,
                        CostOther = model.CostDetails.CostOther,
                        CreatedBy = username,
                        CreatedDate = DateTime.Now
                    });
                }

                // 6. Update Budget Usage
                budget.BudgetUsed += model.EstimatedCost;
                budget.BudgetRemaining -= model.EstimatedCost;
                budget.UpdatedBy = username;
                budget.UpdatedDate = DateTime.Now;

                // 7. Save all changes safely
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
                    throw; // rethrow to catch full exception
                }

                return true;
            }
        }

        public static bool UpdateTravelExpense(TravelExpenseSubmitModel model)
        {
            using (var db = new FinancePortalEntities())
            {
                var username = HttpContext.Current.Session["Username"]?.ToString();

                // 1. Validate TravelExpense exists
                var travel = db.TravelExpenses.FirstOrDefault(t => t.ID == model.ID && t.IsShown);
                if (travel == null)
                    throw new Exception("Travel Expense request not found.");

                // ❗ Block editing if already FC approved (or you can allow only partial fields)
                if (travel.StatusID == (int)TravelExpenseStatusEnum.FCApproved)
                    throw new Exception("Cannot update. This travel expense is already fully approved.");

                // 2. Validate Budget
                var budget = db.TravelExpenseBudgets.FirstOrDefault(b => b.ID == model.BudgetID && b.IsShown);
                if (budget == null)
                    throw new Exception("Selected budget not found or inactive.");

                // 3. Save old estimated cost for budget adjustment
                long oldEstimatedCost = travel.EstimatedCost;

                // 4. Update Cost Details
                var costDetail = db.TravelExpenseCosts.FirstOrDefault(c => c.TravelExpenseID == travel.ID);
                if (costDetail != null)
                {
                    costDetail.CostAir = model.CostDetails?.CostAir ?? 0;
                    costDetail.CostHotel = model.CostDetails?.CostHotel ?? 0;
                    costDetail.CostMeal = model.CostDetails?.CostMeal ?? 0;
                    costDetail.CostOther = model.CostDetails?.CostOther ?? 0;
                    costDetail.UpdatedBy = username;
                    costDetail.UpdatedDate = DateTime.Now;
                }
                else if (model.CostDetails != null)
                {
                    db.TravelExpenseCosts.Add(new TravelExpenseCost
                    {
                        TravelExpenseID = travel.ID,
                        CostAir = model.CostDetails.CostAir,
                        CostHotel = model.CostDetails.CostHotel,
                        CostMeal = model.CostDetails.CostMeal,
                        CostOther = model.CostDetails.CostOther,
                        CreatedBy = username,
                        CreatedDate = DateTime.Now
                    });
                }

                // 5. Recalculate Estimated Cost
                decimal totalCostUSD = (model.CostDetails?.CostAir ?? 0)
                                     + (model.CostDetails?.CostHotel ?? 0)
                                     + (model.CostDetails?.CostMeal ?? 0)
                                     + (model.CostDetails?.CostOther ?? 0);
                long newEstimatedCostVND = (long)(totalCostUSD * (model.ExchangeRate > 0 ? model.ExchangeRate : 1));

                // 6. Update TravelExpense main fields
                travel.FromDate = model.FromDate;
                travel.ToDate = model.ToDate;
                travel.TripDays = model.TripDays;
                travel.RequestDate = model.RequestDate;
                travel.TripPurpose = model.TripPurpose;
                travel.ExchangeRate = model.ExchangeRate;
                travel.EstimatedCost = newEstimatedCostVND;
                travel.BudgetID = model.BudgetID;
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

                // 9. Update Budget Usage
                if (budget != null)
                {
                    // Reverse old usage
                    budget.BudgetUsed -= oldEstimatedCost;
                    budget.BudgetRemaining += oldEstimatedCost;

                    // Apply new usage
                    budget.BudgetUsed += newEstimatedCostVND;
                    budget.BudgetRemaining -= newEstimatedCostVND;

                    budget.UpdatedBy = username;
                    budget.UpdatedDate = DateTime.Now;
                }

                travel.BudgetAmountAtSubmit = budget.BudgetAmount;
                travel.BudgetUsedAtSubmit = budget.BudgetUsed;
                travel.BudgetRemainingAtSubmit = budget.BudgetRemaining;

                // 10. Save Changes
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

                if (travel.StatusID != (int)TravelExpenseStatusEnum.HODPending)
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
                travel.StatusID = (int)TravelExpenseStatusEnum.HODApproved;
                travel.UpdatedBy = approverName;
                travel.UpdatedDate = DateTime.Now;

                db.SaveChanges();
                return true;
            }
        }

        #endregion

        #region Expense Budget

        public static bool AddBudget(TravelExpenseBudget model, out string message)
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
                    b.ID != model.ID);

                if (isDuplicate)
                {
                    message = "Budget name already exists.";
                    return false;
                }

                if (model.ID == 0)
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
                }
                else
                {
                    // ✏️ Update existing
                    var existing = db.TravelExpenseBudgets.FirstOrDefault(b => b.ID == model.ID);
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
                var query = from t in db.TravelExpenses
                            join s in db.TravelExpenseStatus on t.StatusID equals s.ID
                            where t.IsShown == true
                            select new
                            {
                                t.ID,
                                t.TarNo,
                                t.TripPurpose,
                                t.RequestDate,
                                t.EstimatedCost,
                                t.CreatedBy,
                                s.DisplayName,
                                s.ColorCode
                            };

                if (role == RequesterRole)
                {
                    query = query.Where(t => t.CreatedBy == username);
                }
                else if (role == HODRole)
                {
                    query = query.Where(t =>
                        db.TravelExpenseApprovals.Any(a =>
                            a.TravelExpenseID == t.ID &&
                            a.ApprovalStep == (int)ApprovalStep.HOD &&
                            a.ApproverID == employeeCode));
                }
                else if (role == FCRole)
                {
                    query = query.Where(t =>
                        db.TravelExpenseApprovals.Any(a =>
                            a.TravelExpenseID == t.ID &&
                            a.ApprovalStep == (int)ApprovalStep.FC &&
                            a.ApproverID == employeeCode));
                }

                return query
                    .OrderByDescending(t => t.RequestDate)
                    .Take(maxItems)
                    .ToList<object>();
            }
        }

        public static TravelExpenseViewModel GetRequestViewById(int id)
        {
            using (var db = new FinancePortalEntities())
            {
                var t = db.TravelExpenses.FirstOrDefault(x => x.ID == id);
                if (t == null) return null;

                var s = db.TravelExpenseStatus.FirstOrDefault(x => x.ID == t.StatusID);
                var budget = db.TravelExpenseBudgets.FirstOrDefault(b => b.ID == t.BudgetID);

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

                    Budget = budget != null ? new BudgetViewModel
                    {
                        BudgetID = budget.ID,
                        BudgetName = budget.BudgetName,
                        BudgetAmount = budget.BudgetAmount,
                        BudgetUsed = budget.BudgetUsed,
                        BudgetRemaining = budget.BudgetRemaining
                    } : null,

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

                    CostDetails = db.TravelExpenseCosts
                                    .Where(c => c.TravelExpenseID == t.ID)
                                    .Select(c => new TravelExpenseCostViewModel
                                    {
                                        CostAir = c.CostAir,
                                        CostHotel = c.CostHotel,
                                        CostMeal = c.CostMeal,
                                        CostOther = c.CostOther
                                    }).FirstOrDefault() ?? new TravelExpenseCostViewModel(),

                    Approvals = approvals.OrderBy(a => a.ApprovalStep).ToList()
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

                var cost = db.TravelExpenseCosts
                             .Where(c => c.TravelExpenseID == t.ID)
                             .Select(c => new TravelExpenseCostViewModel
                             {
                                 CostAir = c.CostAir,
                                 CostHotel = c.CostHotel,
                                 CostMeal = c.CostMeal,
                                 CostOther = c.CostOther
                             }).FirstOrDefault() ?? new TravelExpenseCostViewModel();

                return new TravelExpenseSubmitModel
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
                    BudgetID = t.BudgetID,
                    CostDetails = cost,
                    Employees = employees,
                    Approver = approver
                };
            }
        }

        public static void UpdateStatusWhenViewingRequests(string role, string employeeCode, string username)
        {
            using (var db = new FinancePortalEntities())
            {
                var now = DateTime.Now;

                if (role == HODRole)
                {
                    // 🔄 Update all requests where approval step = 1 and current status = RequesterPending
                    var pendingRequests = (from t in db.TravelExpenses
                                           join a in db.TravelExpenseApprovals on t.ID equals a.TravelExpenseID
                                           where a.ApproverID == employeeCode &&
                                                 a.ApprovalStep == (int)ApprovalStep.HOD && t.IsShown == true &&
                                                 t.StatusID == (int)TravelExpenseStatusEnum.RequesterPending
                                           select t).ToList();

                    foreach (var req in pendingRequests)
                    {
                        req.StatusID = (int)TravelExpenseStatusEnum.HODPending;
                        //req.UpdatedDate = now;
                    }
                }
                else if (role == GLRole)
                {
                    // 🔄 Update all requests where approval step = 2 and current status = HODApproved
                    var pendingRequests = (from t in db.TravelExpenses
                                           join a in db.TravelExpenseApprovals on t.ID equals a.TravelExpenseID
                                           where a.ApproverID == employeeCode &&
                                                 a.ApprovalStep == (int)ApprovalStep.GL && t.IsShown == true &&
                                                 t.StatusID == (int)TravelExpenseStatusEnum.HODApproved
                                           select t).ToList();

                    foreach (var req in pendingRequests)
                    {
                        req.StatusID = (int)TravelExpenseStatusEnum.GLPending;
                    }
                }
                else if (role == FCRole)
                {
                    // 🔄 Update all requests where approval step = 2 and current status = GLApproved
                    var pendingRequests = (from t in db.TravelExpenses
                                           join a in db.TravelExpenseApprovals on t.ID equals a.TravelExpenseID
                                           where a.ApproverID == employeeCode &&
                                                 a.ApprovalStep == (int)ApprovalStep.FC && t.IsShown == true &&
                                                 t.StatusID == (int)TravelExpenseStatusEnum.GLApproved
                                           select t).ToList();

                    foreach (var req in pendingRequests)
                    {
                        req.StatusID = (int)TravelExpenseStatusEnum.FCPending;
                        //req.UpdatedDate = now;
                    }
                }
                else if (role == RequesterRole)
                {
                    // ✅ Update all of the requester’s own requests that are FCApproved to DONE
                    var approvedRequests = db.TravelExpenses
                        .Where(t => t.CreatedBy == username &&
                                    t.IsShown &&
                                    t.StatusID == (int)TravelExpenseStatusEnum.FCApproved)
                        .ToList();

                    foreach (var req in approvedRequests)
                    {
                        req.StatusID = (int)TravelExpenseStatusEnum.Done;
                        //req.UpdatedDate = now;
                    }
                }
                else
                {
                    return;
                }
                db.SaveChanges();
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
                            .Where(u => u.UserRoles.Any(r => r.Role.RoleName == GLRole &&  r.IsShown == true))
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
                    ? (int)TravelExpenseStatusEnum.HODApproved
                    : (int)TravelExpenseStatusEnum.HODRejected;
                request.UpdatedBy = approverUsername;
                request.UpdatedDate = now;


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
                        }
                    }
                }

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
                            .Where(u => u.UserRoles.Any(r => r.Role.RoleName == FCRole && r.IsShown == true ))
                            .FirstOrDefault();

                // Update approval
                approval.ApproverSignature = approverUsername;
                approval.ApproverSignDate = now;
                approval.IsApproved = isApprove;
                approval.UpdatedDate = now;
                approval.UpdatedBy = approverUsername;

                request.StatusID = isApprove
                    ? (int)TravelExpenseStatusEnum.GLApproved
                    : (int)TravelExpenseStatusEnum.GLPending;
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
                    ? (int)TravelExpenseStatusEnum.FCApproved
                    : (int)TravelExpenseStatusEnum.FCRejected;
                request.UpdatedBy = approverUsername;
                request.UpdatedDate = now;

                db.SaveChanges();

                return new OperationResult
                {
                    Success = true,
                    Message = isApprove ? "Request approved successfully." : "Request rejected successfully."
                };
            }
        }


        #endregion
    }
}