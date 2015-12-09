using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Mailer.Code
{
  /// <summary>
  /// Defines a request for file attachment changes.
  /// </summary>
  [DataContract(Namespace = "https://www.bankhapoalim.co.il/")]
  public class AttachmentRequest
  {
    /// <summary>
    /// Gets and sets the corresponding message ID.
    /// </summary>
    [DataMember(IsRequired = true)]
    public string MessageID { get; set; }

    /// <summary>
    /// Gets and sets the file attachment name.
    /// </summary>
    [DataMember]
    public string Name { get; set; }

    /// <summary>
    /// Gets and sets the file attachment's base64 encoded content.
    /// </summary>
    [DataMember]
    public string Content { get; set; }
  }
}
