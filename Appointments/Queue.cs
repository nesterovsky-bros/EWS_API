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
    
    public partial class Queue
    {
        public long ID { get; set; }
        public string Operation { get; set; }
        public string Request { get; set; }
        public string Response { get; set; }
        public string Error { get; set; }
        public System.DateTime CreatedAt { get; set; }
        public Nullable<System.DateTime> ExpiresAt { get; set; }
    }
}
