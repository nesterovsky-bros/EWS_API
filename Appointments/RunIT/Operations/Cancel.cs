using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Web;

namespace Bnhp.Office365.RunIT.Operations
{
  [DataContract(Namespace = "https://www.bankhapoalim.co.il/")]
  public class Cancel
  {
    [DataMember]
    public string email {get; set;}

    [DataMember]
    public string UID { get; set; }

    [DataMember]
    public string reason { get; set; }
  }

  [DataContract(Namespace = "https://www.bankhapoalim.co.il/")]
  public class CancelResponse
  {
    [DataMember]
    public bool CancelResult { get; set; }
  }
}