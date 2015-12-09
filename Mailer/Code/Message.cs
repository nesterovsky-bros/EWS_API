using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Mailer.Models;

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
    /// Gets and sets the message From field.
    /// </summary>
    [DataMember]
    public BankUser From { get; set; }

    /// <summary>
    /// Gets and sets the message To recipients.
    /// </summary>
    [DataMember]
    public BankUser[] To { get; set; }

    /// <summary>
    /// Gets and sets the message file attachments.
    /// </summary>
    [DataMember]
    public Attachment[] Attachments { get; set; }
  }
}
