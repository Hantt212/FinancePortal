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

        public ActionResult Index(int? id)
        {
            if (id.HasValue)
            {
                var model = TravelExpenseDao.GetSubmitModelById(id.Value);
                return View(model); // edit mode
            }

            return View(); // create new
        }

       
        #endregion

    }
}