using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Mailer.Code
{
  /// <summary>
  /// Defines a recipient.
  /// </summary>
  [DataContract(Namespace = "https://www.bankhapoalim.co.il/")]
  public class Recipient
  {
    /// <summary>
    /// Gets and sets the recipient identity.
    /// </summary>
    [DataMember]
    public string Id { get; set; }

    /// <summary>
    /// Gets and sets the recipient's first and last name.
    /// </summary>
    [DataMember]
    public string Name { get; set; }

    /// <summary>
    /// Gets and sets the recipient's e-mail address.
    /// </summary>
    [DataMember]
    public string EMail { get; set; }

    /// <summary>
    /// Gets and sets the recipient's pairs of division/role.
    /// </summary>
    [DataMember]
    public string[] Roles { get; set; }
  }
}
