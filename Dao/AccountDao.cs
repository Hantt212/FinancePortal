using FinancePortal.Models;
using FinancePortal.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace FinancePortal.Dao
{
	public class AccountDao
	{
        public static List<object> GetAllUsers()
        {
            using (var db = new FinancePortalEntities())
            {
                return db.Users
                    .Where(u => u.IsShown)
                    .OrderByDescending(u => u.CreatedTime)
                    .Select(u => new
                    {
                        u.UserId,
                        u.UserName,
                        u.EmployeeCode,
                        u.UserEmailAddress,
                        u.IsWindowsAccount,
                        u.IsActive,
                        u.LastLogin,
                        u.CreatedTime,
                        RoleName = db.UserRoles
                    .Where(ur => ur.UserId == u.UserId && ur.IsShown == true)
                    .Join(db.Roles.Where(r => r.IsShown == true),
                          ur => ur.RoleId,
                          r => r.RoleId,
                          (ur, r) => r.RoleName)
                    })
                    .ToList<object>();
            }
        }

        public static List<SelectListItem> GetAllRoles()
        {
            using (var db = new FinancePortalEntities())
            {
                return db.Roles
                    .Where(r => r.IsShown == true)
                    .Select(r => new SelectListItem
                    {
                        Text = r.RoleName,
                        Value = r.RoleId.ToString()
                    })
                    .ToList();
            }
        }

        public static UserAccountViewModel GetUserById(int userId)
        {
            using (var db = new FinancePortalEntities())
            {
                var user = db.Users.FirstOrDefault(u => u.UserId == userId && u.IsShown);
                if (user == null)
                    return null;

                var model = new UserAccountViewModel
                {
                    UserId = user.UserId,
                    UserName = user.UserName,
                    EmployeeCode = user.EmployeeCode,
                    UserEmailAddress = user.UserEmailAddress,
                    IsWindowsAccount = user.IsWindowsAccount,
                    IsActive = user.IsActive,
                    SelectedRoleIds = db.UserRoles
                        .Where(ur => ur.UserId == user.UserId && ur.IsShown == true)
                        .Select(ur => ur.RoleId)
                        .ToList(),
                    AvailableRoles = db.Roles
                        .Where(r => r.IsShown == true)
                        .Select(r => new SelectListItem
                        {
                            Text = r.RoleName,
                            Value = r.RoleId.ToString()
                        })
                        .ToList()
                };

                return model;
            }
        }

        public static bool SaveUser(UserAccountViewModel model, out string message)
        {
            message = "";

            using (var db = new FinancePortalEntities())
            {
                string userName = model.UserName?.Trim().ToLower();
                string employeeCode = model.EmployeeCode?.Trim();

                if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(employeeCode))
                {
                    message = "Username and Employee Code are required.";
                    return false;
                }

                // Check for duplicate username
                bool usernameExists = db.Users.Any(u => u.UserName.ToLower() == userName && u.IsShown);
                if (usernameExists)
                {
                    message = "duplicate_username";
                    return false;
                }

                // Check for duplicate employee code
                bool employeeExists = db.Users.Any(u => u.EmployeeCode == employeeCode && u.IsShown);
                if (employeeExists)
                {
                    message = "duplicate_employee";
                    return false;
                }

                // Create user
                var user = new User
                {
                    UserName = model.UserName.Trim(),
                    EmployeeCode = model.EmployeeCode.Trim(),
                    UserEmailAddress = model.UserEmailAddress?.Trim(),
                    IsWindowsAccount = model.IsWindowsAccount,
                    Password = model.IsWindowsAccount ? null : model.Password?.Trim(),
                    IsActive = model.IsActive,
                    CreatedTime = DateTime.Now,
                    IsShown = true
                };

                db.Users.Add(user);
                db.SaveChanges();

                if (model.SelectedRoleIds != null)
                {
                    foreach (var roleId in model.SelectedRoleIds)
                    {
                        db.UserRoles.Add(new UserRole
                        {
                            UserId = user.UserId,
                            RoleId = roleId,
                            IsShown = true
                        });
                    }
                }

                db.SaveChanges();
                return true;
            }
        }


        public static bool UpdateUser(UserAccountViewModel model, out string message)
        {
            message = "";

            using (var db = new FinancePortalEntities())
            {
                var user = db.Users.FirstOrDefault(u => u.UserId == model.UserId && u.IsShown);
                if (user == null)
                {
                    message = "User not found.";
                    return false;
                }

                string newUserName = model.UserName?.Trim().ToLower();
                string newEmployeeCode = model.EmployeeCode?.Trim();

                // 🔁 Check duplicate username (exclude current user)
                bool usernameExists = db.Users.Any(u =>
                    u.UserId != user.UserId &&
                    u.UserName.ToLower() == newUserName &&                  
                    u.IsShown);

                if (usernameExists)
                {
                    message = "duplicate_username";
                    return false;
                }

                // 🔁 Check duplicate employee code (exclude current user)
                bool employeeExists = db.Users.Any(u =>
                    u.UserId != user.UserId &&
                    u.EmployeeCode == newEmployeeCode &&                   
                    u.IsShown);

                if (employeeExists)
                {
                    message = "duplicate_employee";
                    return false;
                }

                // 📝 Update fields
                user.UserName = model.UserName?.Trim();
                user.EmployeeCode = model.EmployeeCode?.Trim();
                user.UserEmailAddress = model.UserEmailAddress?.Trim();
                user.IsWindowsAccount = model.IsWindowsAccount;
                user.IsActive = model.IsActive;
                if (!model.IsWindowsAccount)
                    user.Password = model.Password?.Trim();

                // 🔁 Sync Roles
                var existing = db.UserRoles.Where(r => r.UserId == user.UserId).ToList();
                foreach (var ur in existing)
                    ur.IsShown = false;

                if (model.SelectedRoleIds != null)
                {
                    foreach (var roleId in model.SelectedRoleIds)
                    {
                        var match = existing.FirstOrDefault(x => x.RoleId == roleId);
                        if (match != null)
                            match.IsShown = true;
                        else
                            db.UserRoles.Add(new UserRole
                            {
                                UserId = user.UserId,
                                RoleId = roleId,
                                IsShown = true
                            });
                    }
                }

                db.SaveChanges();
                return true;
            }
        }
    }
}