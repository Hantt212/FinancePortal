using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FinancePortal.ViewModels
{
	public class EmployeeViewModel
	{
        public string Code { get; set; }
        public string Name { get; set; }
        public string Position { get; set; }
        public string Division { get; set; }
        public string Department { get; set; }
        public string Email { get; set; }
        public byte[] ImagePath { get; set; }
        public string EmployeeImage { get; set; }
    }
}