﻿//------------------------------------------------------------------------------
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
    using System.Data.Entity;
    using System.Data.Entity.Infrastructure;
    
    public partial class FinancePortalEntities : DbContext
    {
        public FinancePortalEntities()
            : base("name=FinancePortalEntities")
        {
        }
    
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            throw new UnintentionalCodeFirstException();
        }
    
        public virtual DbSet<CashInAdvance> CashInAdvances { get; set; }
        public virtual DbSet<CashInAdvanceApproval> CashInAdvanceApprovals { get; set; }
        public virtual DbSet<ExpenseClaim> ExpenseClaims { get; set; }
        public virtual DbSet<ExpenseClaimApproval> ExpenseClaimApprovals { get; set; }
        public virtual DbSet<ExpenseClaimDetail> ExpenseClaimDetails { get; set; }
        public virtual DbSet<Payment> Payments { get; set; }
        public virtual DbSet<PaymentDetail> PaymentDetails { get; set; }
        public virtual DbSet<Role> Roles { get; set; }
        public virtual DbSet<sysdiagram> sysdiagrams { get; set; }
        public virtual DbSet<TravelExpense> TravelExpenses { get; set; }
        public virtual DbSet<TravelExpenseApproval> TravelExpenseApprovals { get; set; }
        public virtual DbSet<TravelExpenseAttachmentFile> TravelExpenseAttachmentFiles { get; set; }
        public virtual DbSet<TravelExpenseBudget> TravelExpenseBudgets { get; set; }
        public virtual DbSet<TravelExpenseCost> TravelExpenseCosts { get; set; }
        public virtual DbSet<TravelExpenseCostBudget> TravelExpenseCostBudgets { get; set; }
        public virtual DbSet<TravelExpenseCostDetail> TravelExpenseCostDetails { get; set; }
        public virtual DbSet<TravelExpenseEmployee> TravelExpenseEmployees { get; set; }
        public virtual DbSet<TravelExpenseStatu> TravelExpenseStatus { get; set; }
        public virtual DbSet<User> Users { get; set; }
        public virtual DbSet<UserRole> UserRoles { get; set; }
    }
}
