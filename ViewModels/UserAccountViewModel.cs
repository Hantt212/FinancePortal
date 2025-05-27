using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace FinancePortal.ViewModels
{
    public class UserAccountViewModel
    {
        public int? UserId { get; set; }
        public string UserName { get; set; }
        public string EmployeeCode { get; set; }
        public string UserEmailAddress { get; set; }
        public string Password { get; set; } // Only used if not a Windows account
        public bool IsWindowsAccount { get; set; }
        public bool IsActive { get; set; }
        public string RoleName { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime LastLogin { get; set; }
        public List<int> SelectedRoleIds { get; set; }
        public List<SelectListItem> AvailableRoles { get; set; }
    }

}