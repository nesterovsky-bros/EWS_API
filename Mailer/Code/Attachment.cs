using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Mailer.Code
{
  /// <summary>
  /// Defines a file attachment.
  /// </summary>
  [DataContract(Namespace = "https://www.bankhapoalim.co.il/")]
  public class Attachment
  {
    /// <summary>
    /// Gets and sets the file attachment name.
    /// </summary>
    [DataMember]
    public string Name { get; set; }

    /// <summary>
    /// Gets and sets the file attachment size.
    /// </summary>
    [DataMember]
    public int Size { get; set; }

    /// <summary>
    /// Gets and sets the file attachment's base64 encoded content.
    /// </summary>
    [DataMember]
    public string Content { get; set; }
  }
}
