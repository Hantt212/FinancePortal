//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace FinancePortal.Models
{
    using System;
    using System.Collections.Generic;
    
    public partial class TravelExpenseCost
    {
        public int ID { get; set; }
        public int TravelExpenseID { get; set; }
        public long CostAir { get; set; }
        public long CostHotel { get; set; }
        public long CostMeal { get; set; }
        public long CostOther { get; set; }
        public string CreatedBy { get; set; }
        public System.DateTime CreatedDate { get; set; }
        public string UpdatedBy { get; set; }
        public Nullable<System.DateTime> UpdatedDate { get; set; }
    
        public virtual TravelExpense TravelExpense { get; set; }
    }
}
