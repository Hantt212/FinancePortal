using FinancePortal.Dao;
using FinancePortal.Models;
using FinancePortal.ViewModels;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace FinancePortal.Controllers
{
    public class TravelExpenseController : Controller
    {
        private FinancePortalEntities financeEntity = new FinancePortalEntities();
        private AnnualEmployeeEntities employeeEntity = new AnnualEmployeeEntities();

        #region Expense Form

        public ActionResult Index(int? id)
        {
            if (id.HasValue)
            {
                var model = TravelExpenseDao.GetSubmitModelById(id.Value);
                return View(model); // edit mode
            }

            return View(); // create new
        }

        [HttpGet]
        public JsonResult GetEmployeeByCode(string code)
        {
            if (string.IsNullOrEmpty(code))
                return Json(new { success = false, message = "Employee code is required." }, JsonRequestBehavior.AllowGet);

            var employee = TravelExpenseDao.GetEmployeeByCode(code);

            if (employee == null)
                return Json(new { success = false, message = "Employee not found." }, JsonRequestBehavior.AllowGet);

            return Json(new { success = true, data = employee }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        //public JsonResult SubmitForm(TravelExpenseSubmitModel model)
        public ActionResult SubmitForm(IEnumerable<HttpPostedFileBase> attachmentFiles)
        {
            var payloadJson = Request.Form["Payload"];
            var model = JsonConvert.DeserializeObject<TravelExpenseSubmitModel>(payloadJson);

            if (model == null)
                return Json(new { success = false, message = "Invalid data." });

            try
            {
                // Attachment files
                List<string> fileList = new List<string>();
                foreach (var file in attachmentFiles ?? Enumerable.Empty<HttpPostedFileBase>())
                {
                    if (file != null && file.ContentLength > 0)
                    {
                        var fileName = Path.GetFileName(file.FileName);
                        var path = Path.Combine(Server.MapPath("~/Upload"), fileName);
                        file.SaveAs(path);

                        fileList.Add(fileName);
                    }
                }

                bool result;

                if (model.ID > 0)
                {
                    // Update existing request
                    result = TravelExpenseDao.UpdateTravelExpense(model);
                }
                else
                {
                    // Create new request
                    result = TravelExpenseDao.SaveTravelExpense(model, fileList);
                }

                return Json(new { success = result, message = result ? "" : "Failed to save travel expense." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Server error: " + ex.Message });
            }
        }

        public JsonResult GetBudgets()
        {
            var budgets = TravelExpenseDao.GetShownBudgets();            
             
            return Json(budgets, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult GenerateTARNo()
        {
            string tarNo = TravelExpenseDao.GenerateTARNumber();
            return Json(new { tarNo }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult GetRequesterPreloadInfo()
        {
            var username = System.Web.HttpContext.Current.Session["Username"]?.ToString();

            if (string.IsNullOrWhiteSpace(username))
            {
                return Json(new { success = false, message = "User not found in session." }, JsonRequestBehavior.AllowGet);
            }

            var preload = TravelExpenseDao.GetLatestRequesterPreloadInfo(username);

            return Json(new
            {
                success = true,
                requesterSign = preload.requesterSign,
                requesterSignDate = preload.requesterSignDate?.ToString("yyyy-MM-dd"),
                approver = preload.approverApproval == null ? null : new
                {
                    code = preload.approverApproval.ApproverID,
                    name = preload.approverApproval.ApproverName,
                    email = preload.approverApproval.ApproverEmail,
                    position = preload.approverApproval.ApproverPosition                   
                }
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult GetHodByCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return Json(new { success = false, message = "Approver code is required." }, JsonRequestBehavior.AllowGet);

            var user = financeEntity.Users.FirstOrDefault(u => u.EmployeeCode == code && u.IsShown);
            if (user == null)
                return Json(new { success = false, message = "User not found." }, JsonRequestBehavior.AllowGet);

            var hodRole = financeEntity.UserRoles
                .Any(r => r.UserId == user.UserId && r.IsShown == true && r.Role.RoleName == "HOD");

            if (!hodRole)
                return Json(new { success = false, message = "This user is not a valid HOD." }, JsonRequestBehavior.AllowGet);

            var emp = TravelExpenseDao.GetEmployeeByCode(code);
            return Json(new { success = true, data = emp }, JsonRequestBehavior.AllowGet);
        }

        #endregion

        #region Dashboard

        public ActionResult Dashboard()
        {
            return View();
        }

        public JsonResult GetDashboardStats()
        {
            var stats = TravelExpenseDao.GetDashboardStats();
            return Json(stats, JsonRequestBehavior.AllowGet);
        }

        #endregion

        #region Expense Budget

        public ActionResult ExpenseBudget()
        {
            return View();
        }

        [HttpGet]
        public JsonResult GetAllBudgets()
        {
            try
            {
                var budgets = TravelExpenseDao.GetAllBudgets(); // or GetShownBudgets()
                return Json(budgets, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public JsonResult AddBudget(TravelExpenseBudget model)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(model.BudgetName) || model.BudgetAmount <= 0)
                    return Json(new { success = false, message = "Invalid input." });

                if (TravelExpenseDao.AddBudget(model, out string message))
                    return Json(new { success = true });

                return Json(new { success = false, message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public JsonResult GetBudgetDetails(int id)
        {
            var result = TravelExpenseDao.GetBudgetDetailsById(id);
            if (result == null)
                return Json(new { success = false, message = "Budget not found." }, JsonRequestBehavior.AllowGet);

            return Json(new { success = true, data = result }, JsonRequestBehavior.AllowGet);
        }

        #endregion

        #region Request List

        public ActionResult List()
        {
            return View();
        }

        [HttpGet]
        public JsonResult GetUserRequests()
        {
            string username = System.Web.HttpContext.Current.Session["Username"]?.ToString();
            string employeeCode = System.Web.HttpContext.Current.Session["EmployeeID"]?.ToString();
            string role = System.Web.HttpContext.Current.Session["UserRole"]?.ToString();

            // 🔄 Update statuses based on role
            TravelExpenseDao.UpdateStatusWhenViewingRequests(role, employeeCode, username);

            // ✅ Return filtered list
            var list = TravelExpenseDao.GetRequestSummariesByUser(username, employeeCode, role);
            return Json(list, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult GetRequestViewDetails(int id)
        {
            var result = TravelExpenseDao.GetRequestViewById(id);
            return Json(result, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult ApproveByHOD(ApprovalActionModel dto)
        {
            var username = Session["Username"]?.ToString();
            if (dto == null || dto.requestId <= 0 || string.IsNullOrEmpty(username))
            {
                return Json(new { success = false, message = "Invalid request or session." });
            }

            var result = TravelExpenseDao.HandleHODApproval(dto.requestId, username, dto.isApprove);

            return Json(new { success = result.Success, message = result.Message });
        }

        [HttpPost]
        public JsonResult ApproveByGL(ApprovalActionModel dto)
        {
            var username = Session["Username"]?.ToString();
            if (dto == null || dto.requestId <= 0 || string.IsNullOrEmpty(username))
            {
                return Json(new { success = false, message = "Invalid request or session." });
            }

            var result = TravelExpenseDao.HandleGLApproval(dto.requestId, username, dto.isApprove);

            return Json(new { success = result.Success, message = result.Message });
        }

        [HttpPost]
        public JsonResult ApproveByFC(ApprovalActionModel dto)
        {
            var username = Session["Username"]?.ToString();
            if (dto == null || dto.requestId <= 0 || string.IsNullOrEmpty(username))
            {
                return Json(new { success = false, message = "Invalid request or session." });
            }

            var result = TravelExpenseDao.HandleFCApproval(dto.requestId, username, dto.isApprove);

            return Json(new { success = result.Success, message = result.Message });
        }

        [HttpPost]
        public JsonResult CancelByRequester(int requestID)
        {
            var username = Session["Username"]?.ToString();
            if (requestID <= 0 || string.IsNullOrEmpty(username))
            {
                return Json(new { success = false, message = "Invalid request or session." });
            }

            var result = TravelExpenseDao.HandleRequesterCancel(requestID, username);

            return Json(new { success = result.Success, message = result.Message });
        }

        #endregion
    }
}