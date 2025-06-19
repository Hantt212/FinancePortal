using FinancePortal.Dao;
using FinancePortal.Models;
using FinancePortal.ViewModels;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Net;
using System.Web;
using System.Web.Mvc;

namespace FinancePortal.Controllers
{
    public class CashInAdvanceController : Controller
    {
        private FinancePortalEntities financeEntity = new FinancePortalEntities();

        #region CashInAdvanceForm

        public ActionResult Index(string t)
        {
            if (string.IsNullOrEmpty(t))
                return RedirectToAction("AccessDenied");

            try
            {
                var decryptedId = TokenHelper.Decrypt(t);
                int travelID = int.Parse(decryptedId);

                var model = CashInAdvanceDao.GetCashInAdvanceByTravelID(travelID);
              
                if (model == null)
                    return RedirectToAction("AccessDenied");

                return View(model);
            }
            catch
            {
                return RedirectToAction("AccessDenied");
            }
        }


        [HttpPost]
        public JsonResult SubmitForm(CashInAdvanceViewModel model)
        {
            if (model == null)
                return Json(new { success = false, message = "Invalid data." });

            try
            {
                bool result = false;
                if (model.ID > 0)
                {
                    // Update existing request
                    result = CashInAdvanceDao.UpdateCashInAdvance(model);
                }
                else
                {
                    // Create new request
                    result = CashInAdvanceDao.SaveCashInAdvance(model);
                }

                return Json(new { success = result, message = result ? "" : "Failed to save travel expense." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Server error: " + ex.Message });
            }

           // return Json(new { success = result, message = result ? "" : "Failed to save travel expense." });
        }

        [HttpGet]
        public JsonResult GetCIAViewDetails(int id)
        {
            var result = CashInAdvanceDao.GetCIAViewDetails(id);
            return Json(result, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult HandleCIAByHOD(ApprovalActionModel dto)
        {
            var username = Session["Username"]?.ToString();
            if (dto == null || dto.requestId <= 0 || string.IsNullOrEmpty(username))
            {
                return Json(new { success = false, message = "Invalid request or session." });
            }

            var result = CashInAdvanceDao.HandleCIAByHOD(dto.requestId, username, dto.isApprove);

            return Json(new { success = result.Success, message = result.Message });
        }

        [HttpPost]
        public JsonResult HandleCIAByAP(ApprovalActionModel dto)
        {
            var username = Session["Username"]?.ToString();
            if (dto == null || dto.requestId <= 0 || string.IsNullOrEmpty(username))
            {
                return Json(new { success = false, message = "Invalid request or session." });
            }

            var result = CashInAdvanceDao.HandleCIAByAP(dto.requestId, username, dto.isApprove);

            return Json(new { success = result.Success, message = result.Message });
        }

        [HttpPost]
        public JsonResult HandleCIAByFC(ApprovalActionModel dto)
        {
            var username = Session["Username"]?.ToString();
            if (dto == null || dto.requestId <= 0 || string.IsNullOrEmpty(username))
            {
                return Json(new { success = false, message = "Invalid request or session." });
            }

            var result = CashInAdvanceDao.HandleCIAByFC(dto.requestId, username, dto.isApprove);

            return Json(new { success = result.Success, message = result.Message });
        }

        [HttpPost]
        public JsonResult HandleCIAByRequester(int requestID)
        {
            var username = Session["Username"]?.ToString();
            if (requestID <= 0 || string.IsNullOrEmpty(username))
            {
                return Json(new { success = false, message = "Invalid request or session." });
            }

            var result = CashInAdvanceDao.HandleCIAByRequester(requestID, username);

            return Json(new { success = result.Success, message = result.Message });
        }
        #endregion

    }
}