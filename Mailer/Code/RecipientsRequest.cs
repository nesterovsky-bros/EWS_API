using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Mailer.Code
{
  /// <summary>
  /// Defines a request to find recipients.
  /// </summary>
  [DataContract(Namespace = "https://www.bankhapoalim.co.il/")]
  public class RecipientsRequest
  {
    /// <summary>
    /// Gets and sets hierarchy IDs.
    /// </summary>
    [DataMember]
    public string[] HierarchyIDs { get; set; }

    /// <summary>
    /// Gets and sets roles names.
    /// </summary>
    [DataMember]
    public string[] Roles { get; set; }
  }
}
