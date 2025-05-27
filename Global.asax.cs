using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using System.Web.Security;

namespace FinancePortal
{
    public class MvcApplication : System.Web.HttpApplication
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }

        protected void Application_AuthenticateRequest(object sender, EventArgs e)
        {
            if (HttpContext.Current.User?.Identity?.IsAuthenticated == true)
            {
                var identity = HttpContext.Current.User.Identity as FormsIdentity;
                if (identity?.Ticket != null)
                {
                    var roles = identity.Ticket.UserData.Split(',');
                    HttpContext.Current.User = new System.Security.Principal.GenericPrincipal(identity, roles);
                }
            }
        }

        protected void Application_Error()
        {
            var exception = Server.GetLastError();
            logger.Error(exception, "Unhandled exception occurred");
        }
    }
}
