﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Web;

namespace Bnhp.Office365.RunIT.Operations
{
  [DataContract(Namespace = "https://www.bankhapoalim.co.il/")]
  public class Accept
  {
    [DataMember]
    public string email {get; set;}

    [DataMember]
    public string UID { get; set; }
  }

  [DataContract(Namespace = "https://www.bankhapoalim.co.il/")]
  public class AcceptResponse
  {
    [DataMember]
    public bool AcceptResult { get; set; }
  }
}