using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Web;

namespace Bnhp.Office365.RunIT.Operations
{
  [DataContract(Namespace = "https://www.bankhapoalim.co.il/")]
  public class Get
  {
    [DataMember]
    public string email {get; set;}

    [DataMember]
    public string UID { get; set; }
  }

  [DataContract(Namespace = "https://www.bankhapoalim.co.il/")]
  public class GetResponse
  {
    [DataMember]
    public Appointment GetResult { get; set; }
  }
}