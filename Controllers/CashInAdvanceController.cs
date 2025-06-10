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

        //public ActionResult Index(int? id)
        //{
        //    if (id.HasValue)
        //    {
        //        var model = TravelExpenseDao.GetSubmitModelById(id.Value);
        //        return View(model); // edit mode
        //    }

        //    return View(); // create new
        //}

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
                  //  result = TravelExpenseDao.UpdateTravelExpense(model, newAttachFiles);
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
            #endregion

        }
}