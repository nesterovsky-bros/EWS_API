﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Web;

namespace Bnhp.Office365
{
  /// <summary>
  /// A proxy class for Office 365 item (e-mail or appointment).
  /// </summary>
  /// <seealso cref="https://msdn.microsoft.com/en-us/library/microsoft.exchange.webservices.data.appointment_properties(v=exchg.80).aspx"/>
  [DataContract(Namespace = "https://www.bankhapoalim.co.il/")]
  public class Item
  {
    /// <summary>
    /// Gets the ID of this item. 
    /// </summary>
    [DataMember]
    public string Id { get; internal set; }

    /// <summary>
    /// Gets a text summarizing the To recipients of this item.
    /// </summary>
    [DataMember]
    public string DisplayTo { get; internal set; }
  
    /// <summary>
    /// Gets the date and time this item was created.
    /// </summary>
    [DataMember]
    public DateTime? DateTimeCreated { get; internal set; }

    /// <summary>
    /// Gets the time when this item was received.
    /// </summary>
    [DataMember]
    public DateTime? DateTimeReceived { get; internal set; }
    
    /// <summary>
    /// Gets the date and time this item was sent.
    /// </summary>
    [DataMember]
    public DateTime? DateTimeSent { get; internal set; }
    
    /// <summary>
    /// Gets a text summarizing the Cc receipients of this item.
    /// </summary>
    [DataMember]
    public string DisplayCc { get; internal set; }

    /// <summary>
    /// Gets a value indicating whether the item has been modified since it was created.
    /// </summary>
    [DefaultValue(false)]
    [DataMember]
    public bool IsUnmodified { get; internal set;  }

    /// <summary>
    /// Gets and sets importance of the appointment.
    /// </summary>
    [DefaultValue(Importance.Normal)]
    [DataMember]
    public Importance Importance { get; set; }

    /// <summary>
    /// Gets the name of the user who last modified this item.
    /// </summary>
    [DataMember]
    public string LastModifiedName { get; internal set; }

    /// <summary>
    /// Gets the date and time this item was last modified.
    /// </summary>
    [DataMember]
    public DateTime LastModifiedTime { get; internal set; }

    /// <summary>
    /// Gets or sets the subject of this item.
    /// </summary>
    [DataMember]
    public string Subject { get; set; }

    /// <summary>
    /// Gets the sensitivity of this item.
    /// </summary>
    [DataMember]
    public Sensitivity Sensitivity { get; set; }

    /// <summary>
    /// Gets the text body of the item as a string value.
    /// </summary>
    [DataMember]
    public string TextBody { get; set; }
  }

  /// <summary>
  /// Represents an e-mail address.
  /// </summary>
  [DataContract(Namespace = "https://www.bankhapoalim.co.il/")]
  public class EMailAddress
  {
    /// <summary>
    /// Gets or sets the name that is associated with the email address. 
    /// </summary>
    [DataMember]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the email address.
    /// </summary>
    [DataMember]
    public string Address { get; set; }

    /// <summary>
    /// Converts Attendee instance to a string value.
    /// </summary>
    /// <returns>a string value that represents this Attendee instance.</returns>
    public override string ToString()
    {
      return Name + " <" + Address + ">";
    }
  }

  /// <summary>
  /// Defines an appointment sensitivity.
  /// </summary>
  [DataContract(Namespace = "https://www.bankhapoalim.co.il/")]
  public enum Sensitivity
  {
    /// <summary>
    /// The item has a normal sensitivity.
    /// </summary>
    [EnumMember]
    Normal,

    /// <summary>
    /// The item is personal.
    /// </summary>
    [EnumMember]
    Personal,

    /// <summary>
    /// The item is private.
    /// </summary>
    [EnumMember]
    Private,

    /// <summary>
    /// The item is confidential.
    /// </summary>
    [EnumMember]
    Confidential
  }

  /// <summary>
  /// 
  /// </summary>
  [DataContract(Namespace = "https://www.bankhapoalim.co.il/")]
  public enum Importance
  {
    /// <summary>
    /// Low importance.
    /// </summary>
    [EnumMember]
    Low,

    /// <summary>
    /// Normal importance.
    /// </summary>
    [EnumMember]
    Normal,

    /// <summary>
    /// High importance.
    /// </summary>
    [EnumMember]
    High
  }
}