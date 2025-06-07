using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FinancePortal.ViewModels
{
    public class MailContentModel
    {
        public string TarNumber { get; set; }
        public string Status { get; set; }
        public string RecipientPosition{ get; set; }
        public string RecipientTo { get; set; }
        public string RecipientCc { get; set; }

        public string Content { get; set; }
    }
}