﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Bnhp.Office365
{
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Infrastructure;
    
    public partial class EWSQueueEntities : DbContext
    {
        public EWSQueueEntities()
            : base("name=EWSQueueEntities")
        {
        }
    
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            throw new UnintentionalCodeFirstException();
        }
    
        public virtual DbSet<Queue> Queues { get; set; }
        public virtual DbSet<MailboxAffinity> MailboxAffinities { get; set; }
        public virtual DbSet<BankMailbox> BankMailboxes { get; set; }
        public virtual DbSet<BankNotification> BankNotifications { get; set; }
        public virtual DbSet<BankSystem> BankSystems { get; set; }
        public virtual DbSet<SystemManager> SystemManagers { get; set; }
        public virtual DbSet<WorkTable> WorkTables { get; set; }
    }
}
