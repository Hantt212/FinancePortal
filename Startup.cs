using Microsoft.Owin;
using Owin;

[assembly: OwinStartup(typeof(FinancePortal.Startup))]

namespace FinancePortal
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }

        private void ConfigureAuth(IAppBuilder app)
        {
            // Enable cookie authentication (needed for IAuthenticationManager)
            app.UseCookieAuthentication(new Microsoft.Owin.Security.Cookies.CookieAuthenticationOptions
            {
                AuthenticationType = Microsoft.AspNet.Identity.DefaultAuthenticationTypes.ApplicationCookie,
                LoginPath = new PathString("/Account/Login"),
                LogoutPath = new PathString("/Account/LogOff"),
                ExpireTimeSpan = System.TimeSpan.FromHours(4),
                SlidingExpiration = true
            });

            // Optional: Use external sign-in cookie for third-party auth if needed
            app.UseExternalSignInCookie(Microsoft.AspNet.Identity.DefaultAuthenticationTypes.ExternalCookie);
        }
    }
}
