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
  public class Addressee
  {
    /// <summary>
    /// Gets and sets the addressee identity.
    /// </summary>
    [DataMember]
    public string Id { get; set; }

    /// <summary>
    /// Gets and sets the addressee's first and last name or role and division.
    /// </summary>
    [DataMember]
    public string Name { get; set; }

    /// <summary>
    /// Gets and sets the addressee's e-mail address.
    /// </summary>
    [DataMember]
    public string Email { get; set; }

    /// <summary>
    /// A user or group.
    /// </summary>
    [DataMember]
    public string ItemName { get; set; }

    /// <summary>
    /// A bank hierarchy ID.
    /// </summary>
    [DataMember]
    public string HierarchyID { get; set; }
  }
}
