
//------------------------------------------------------------------------------
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
    using System.Collections.Generic;
    
public partial class BankSystem
{

    public BankSystem()
    {

        this.BankNotifications = new HashSet<BankNotification>();

        this.WorkTables = new HashSet<WorkTable>();

    }


    public int systemID { get; set; }

    public string name { get; set; }

    public string userName { get; set; }

    public string description { get; set; }

    public int systemManagerId { get; set; }



    public virtual ICollection<BankNotification> BankNotifications { get; set; }

    public virtual SystemManager SystemManager { get; set; }

    public virtual ICollection<WorkTable> WorkTables { get; set; }

}

}
