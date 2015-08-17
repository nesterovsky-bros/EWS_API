using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Web;

namespace Bnhp.Office365.RunIT.Operations
{
  [DataContract(Namespace = "https://www.bankhapoalim.co.il/")]
  public class Find
  {
    [DataMember]
    public string email {get; set;}

    [DataMember]
    public DateTime start { get; set; }

    [DataMember]
    public DateTime? end { get; set; }

    [DataMember]
    public int? maxResults { get; set; }
  }

  [DataContract(Namespace = "https://www.bankhapoalim.co.il/")]
  public class FindResponse
  {
    [DataMember]
    public List<string> FindResult { get; set; }
  }
}