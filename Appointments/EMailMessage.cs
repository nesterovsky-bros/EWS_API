using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Web;

namespace Bnhp.Office365
{
  /// <summary>
  /// A proxy class for Office 365 e-mail message.
  /// </summary>
  [DataContract(Namespace = "https://www.bankhapoalim.co.il/")]
  public class EMailMessage: Item
  {
    /// <summary>
    /// Gets or sets the "on behalf" sender of the e-mail message.
    /// </summary>
    [DataMember]
    public EMailAddress From { get; set; }

    /// <summary>
    /// Gets or sets the sender of the e-mail message.
    /// </summary>
    [DataMember]
    public EMailAddress Sender { get; set; }

    /// <summary>
    /// Gets the list of Bcc recipients for the e-mail message.
    /// </summary>
    [DataMember]
    public List<EMailAddress> BccRecipients { get; internal set; }

    /// <summary>
    /// Gets the list of Cc recipients for the e-mail message.
    /// </summary>
    [DataMember]
    public List<EMailAddress> CcRecipients { get; internal set; }

    /// <summary>
    /// Gets the list of To recipients for the e-mail message.
    /// </summary>
    [DataMember]
    public List<EMailAddress> ToRecipients { get; internal set; }

    /// <summary>
    /// Gets or sets a value indicating whether responses are requested.
    /// </summary>
    [DataMember]
    public bool? IsResponseRequested { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a read receipt is requested for the
    /// e-mail message.
    /// </summary>
    [DefaultValue(false)]
    [DataMember]
    public bool IsDeliveryReceiptRequested { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the e-mail message is read.
    /// </summary>
    [DefaultValue(false)]
    [DataMember]
    public bool IsRead { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a read receipt is requested for the
    /// e-mail message.
    /// </summary>
    [DefaultValue(false)]
    [DataMember]
    public bool IsReadReceiptRequested { get; set; }

    /// <summary>
    /// Gets and sets list of the file attachments' pathes.
    /// </summary>
    [DataMember]
    public List<Attachment> Attachments { get; internal set; }
  }

  [DataContract(Namespace = "https://www.bankhapoalim.co.il/")]
  public class Attachment
  {
    /// <summary>
    /// Gets or sets a name of the attachment.
    /// </summary>
    [DataMember]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets a content type of the attachment.
    /// </summary>
    [DataMember]
    public string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the attachment's size.
    /// </summary>
    [DataMember]
    public int? Size { get; internal set; }
  }
}