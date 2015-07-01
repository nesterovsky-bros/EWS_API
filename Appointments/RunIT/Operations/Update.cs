using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Web;

namespace Bnhp.Office365.RunIT.Operations
{
  [DataContract(Namespace = "https://www.bankhapoalim.co.il/")]
  public class Update
  {
    [DataMember]
    public string email {get; set;}

    [DataMember]
    public Appointment appointment { get; set; }
  }

  [DataContract(Namespace = "https://www.bankhapoalim.co.il/")]
  public class UpdateResponse
  {
    [DataMember]
    public bool UpdateResult { get; set; }
  }
}