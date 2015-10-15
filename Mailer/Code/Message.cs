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
  public class Message
  {
    /// <summary>
    /// Gets and sets the message subject.
    /// </summary>
    [DataMember]
    public string Subject { get; set; }

    /// <summary>
    /// Gets and sets the message content.
    /// </summary>
    [DataMember]
    public string Content { get; set; }

    /// <summary>
    /// Gets and sets the message To recipients.
    /// </summary>
    [DataMember]
    public Addressee[] To { get; set; }

    /// <summary>
    /// Gets and sets the message Cc recipients.
    /// </summary>
    [DataMember]
    public Addressee[] Cc { get; set; }

    /// <summary>
    /// Gets and sets the message Bcc recipients.
    /// </summary>
    [DataMember]
    public Addressee[] Bcc { get; set; }

    /// <summary>
    /// Gets and sets the message file attachments.
    /// </summary>
    [DataMember]
    public Attachment[] Attachments { get; set; }
  }
}
