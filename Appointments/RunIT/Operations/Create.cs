using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Web;

namespace Bnhp.Office365.RunIT.Operations
{
  [DataContract(Namespace = "https://www.bankhapoalim.co.il/")]
  public class Create
  {
    [DataMember]
    public string email {get; set;}

    [DataMember]
    public Appointment appointment { get; set; }
  }

  [DataContract(Namespace = "https://www.bankhapoalim.co.il/")]
  public class CreateResponse
  {
    [DataMember]
    public string CreateResult { get; set; }
  }
}