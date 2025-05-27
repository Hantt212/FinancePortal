using FinancePortal.Models;
using LeaveRequestForm.Models;
using Microsoft.AspNet.Identity;
using Microsoft.Owin.Security;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using Microsoft.Owin;
using System.DirectoryServices;
using FinancePortal.ViewModels;
using FinancePortal.Dao;

namespace FinancePortal.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public FinancePortalEntities db = new FinancePortalEntities();

        #region Login

        // GET: /Account/Login
        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Login(LoginViewModel model, string returnUrl)
        {
            logger.Info($"Login attempt: {model.UserName}");

            if (!ModelState.IsValid)
            {
                logger.Warn("Model state invalid during login.");
                ModelState.AddModelError("ErrorLogin", "Username or password is invalid");
                return View(model);
            }

            var user = await Task.Run(() =>
            {
                return db.Users.FirstOrDefault(u => u.UserName == model.UserName && u.IsActive);
            });

            if (user == null)
            {
                logger.Warn($"Login failed - user not found or inactive: {model.UserName}");
                ModelState.AddModelError("ErrorLogin", "User does not exist or is inactive");
                return View(model);
            }

            bool isAuthenticated = false;

            if (user.IsWindowsAccount)
            {
                logger.Info($"Attempting Windows authentication for user: {model.UserName}");

                using (DirectoryEntry entry = new DirectoryEntry())
                {
                    entry.Username = model.UserName;
                    entry.Password = model.Password;

                    try
                    {
                        DirectorySearcher searcher = new DirectorySearcher(entry);
                        searcher.Filter = "(objectclass=user)";
                        SearchResult sr = searcher.FindOne();
                        isAuthenticated = (sr != null);

                        if (isAuthenticated)
                            logger.Info($"Windows authentication succeeded for user: {model.UserName}");
                        else
                            logger.Warn($"Windows authentication failed for user: {model.UserName}");
                    }
                    catch (System.Runtime.InteropServices.COMException ex)
                    {
                        logger.Error(ex, $"Windows authentication error for user: {model.UserName}");
                        ModelState.AddModelError("ErrorLogin", "Windows authentication failed.");
                        return View(model);
                    }
                }
            }
            else
            {
                logger.Info($"Attempting local DB authentication for user: {model.UserName}");

                isAuthenticated = user.Password == model.Password;

                if (isAuthenticated)
                    logger.Info($"Local DB authentication succeeded for user: {model.UserName}");
                else
                    logger.Warn($"Local DB authentication failed for user: {model.UserName}");
            }

            if (!isAuthenticated)
            {
                ModelState.AddModelError("ErrorLogin", "Username or password is incorrect");
                return View(model);
            }

            // Get single user role
            var role = db.UserRoles
                         .Where(ur => ur.UserId == user.UserId && ur.IsShown == true)
                         .Select(ur => ur.Role.RoleName)
                         .FirstOrDefault();

            // Setup authentication ticket
            SetupFormsAuthTicket(user.UserName, role, true);

            Session["Username"] = user.UserName;
            Session["UserRole"] = role; // singular
            Session["EmployeeID"] = user.EmployeeCode;


            return RedirectToAction("Dashboard", "Account");
        }

        public ActionResult Dashboard()
        {
            return View();
        }

        public void SetupFormsAuthTicket(string userName, string roles, bool persistanceFlag)
        {
            var authTicket = new FormsAuthenticationTicket(
                1,
                userName,
                DateTime.Now,
                DateTime.Now.AddHours(4),
                persistanceFlag,
                roles
            );

            string encTicket = FormsAuthentication.Encrypt(authTicket);
            var cookie = new HttpCookie(FormsAuthentication.FormsCookieName, encTicket);
            Response.Cookies.Add(cookie);
        }

        public ActionResult LogOff()
        {
            AuthenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
            Session.Clear();
            Session.Abandon();

            HttpCookie rFormsCookie = new HttpCookie(FormsAuthentication.FormsCookieName, "");
            rFormsCookie.Expires = DateTime.Now.AddMinutes(240.0);
            Response.Cookies.Add(rFormsCookie);

            return RedirectToAction("Login", "Account");
        }

        private IAuthenticationManager AuthenticationManager
        {
            get
            {
                return HttpContext.GetOwinContext().Authentication;
            }
        }

        public int isWindowAcc(LoginViewModel model)
        {
            using (DirectoryEntry entry = new DirectoryEntry())
            {
                entry.Username = model.UserName;
                entry.Password = model.Password;

                DirectorySearcher searcher = new DirectorySearcher(entry);

                searcher.Filter = "(objectclass=user)";
                try
                {
                    SearchResult sr = searcher.FindOne();
                    return (sr != null) ? 1 : 0;
                }
                catch (System.Runtime.InteropServices.COMException ex)
                {
                    return -2;
                }
            }
        }

        #endregion

        #region User

        [HttpGet]
        public ActionResult ManageUser()
        {
            var users = AccountDao.GetAllUsers();
            ViewBag.AllRoles = AccountDao.GetAllRoles();

            return View(users);
        }

        [HttpGet]
        public JsonResult GetUserList()
        {
            var users = AccountDao.GetAllUsers();
            return Json(users, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult GetUserById(int id)
        {
            var model = AccountDao.GetUserById(id);
            if (model == null)
                return Json(new { success = false, message = "User not found." }, JsonRequestBehavior.AllowGet);

            return Json(new { success = true, data = model }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult SaveUser(UserAccountViewModel model)
        {
            string message;

            //bool result = model.UserId > 0
            //    ? AccountDao.UpdateUser(model, out message)
            //    : AccountDao.SaveUser(model, out message);

            bool result;
            if (model.UserId > 0)
            {
                System.Diagnostics.Debug.WriteLine("🔁 Updating user with ID: " + model.UserId);
                result = AccountDao.UpdateUser(model, out message);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("➕ Saving new user with EmployeeCode: " + model.EmployeeCode);
                result = AccountDao.SaveUser(model, out message);
            }


            return Json(new { success = result, message });
        }

        #endregion
    }
}